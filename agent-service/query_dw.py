"""
query_dw — local execution of NL→SQL queries against PFA_DW.

Pipeline:
  1. _nl_to_sql: call model_sql (MiniMax M2.7) to generate a T-SQL SELECT.
  2. _validate_sql: L4 regex safety (defense-in-depth; primary guard is the
     db_datareader-only pfa_app_readonly login at the SQL Server level).
  3. _run_query: execute via pyodbc using asyncio.to_thread (non-blocking).
  4. Return the standard {ok, data} envelope or {ok:false, error} on any failure.

pyodbc is imported lazily inside _run_query_sync so the module loads cleanly
in test environments where the ODBC driver stack is absent.
"""
from __future__ import annotations

import asyncio
import re
from contextlib import closing
from typing import Any

from config import Settings
from provider import LLMProvider
from schemas import ChatMessage

# ── DDL context sent to the NL→SQL model ────────────────────────────────────

_DW_SCHEMA = """
DimEtudiant (EtudiantKey INT PK, Matricule NVARCHAR(20), Nom NVARCHAR(100),
             Prenom NVARCHAR(100), Filiere NVARCHAR(100), Niveau NVARCHAR(10))
-- Filiere stores the FULL filière name (e.g. 'Génie Informatique'), not the code.

DimModule   (ModuleKey INT PK, Code NVARCHAR(20), Nom NVARCHAR(200),
             Filiere NVARCHAR(100), Coefficient DECIMAL(4,2))

DimTemps    (TempsKey INT PK, Annee NVARCHAR(9), Semestre NVARCHAR(5))
-- Annee format: '2025/2026'; Semestre: 'S1' or 'S2'.

FaitNotes   (Id INT PK,
             EtudiantKey INT FK->DimEtudiant, ModuleKey INT FK->DimModule,
             TempsKey INT FK->DimTemps,
             NoteFinale DECIMAL(5,2) NULL,   -- grade /20; NULL = not yet graded
             NbAbsences INT DEFAULT 0,
             ScoreRisque DECIMAL(5,4) NULL,  -- ML risk 0-1; NULL = not yet run
             Cluster INT NULL)
"""

_SQL_SYSTEM_PROMPT = (
    "You are a SQL code generator for Microsoft SQL Server (T-SQL). "
    "Output ONLY a single valid T-SQL SELECT statement — no explanation, "
    "no markdown, no prose, no comments. Just the raw SQL.\n\n"
    "Target database: PFA_DW. Use T-SQL (TOP N, not LIMIT). "
    "Do not reference PFA_DB or any database other than PFA_DW.\n\n"
    f"Schema (star schema):\n{_DW_SCHEMA}"
)

# ── L4 safety ───────────────────────────────────────────────────────────────

_BLOCKLIST = re.compile(
    r"\b(INSERT|UPDATE|DELETE|DROP|TRUNCATE|ALTER|CREATE|EXEC|EXECUTE|MERGE"
    r"|GRANT|REVOKE|DENY|BULK|OPENQUERY|OPENROWSET|sp_|xp_)\b",
    re.IGNORECASE,
)
_CROSS_DB = re.compile(r"\bPFA_DB\b", re.IGNORECASE)
_MULTI_STMT = re.compile(r";")


def _validate_sql(sql: str) -> tuple[bool, str]:
    """Return (True, '') or (False, reason). Primary guard is the read-only login."""
    # Strip trailing semicolons: LLMs routinely append one to a single SELECT.
    # A true multi-statement payload ("SELECT 1; SELECT 2") still has a semicolon
    # after stripping the last one, so _MULTI_STMT still catches it.
    s = sql.strip().rstrip(";").rstrip()
    if not s:
        return False, "SQL vide"
    if not re.match(r"^\s*(WITH\b|SELECT\b)", s, re.IGNORECASE):
        return False, "seul SELECT est autorisé"
    m = _BLOCKLIST.search(s)
    if m:
        return False, f"mot-clé interdit: {m.group()}"
    if _CROSS_DB.search(s):
        return False, "référence inter-base interdite (PFA_DB)"
    if _MULTI_STMT.search(s):
        return False, "instructions multiples interdites"
    return True, ""


# ── NL→SQL ──────────────────────────────────────────────────────────────────

_THINK_RE  = re.compile(r"<think>.*?</think>", re.DOTALL | re.IGNORECASE)
_FENCE_RE  = re.compile(r"^```[a-z]*\n?(.*?)```$", re.DOTALL)


def _clean_model_output(raw: str) -> str:
    """Strip <think> blocks, markdown fences, and trailing semicolons from model output."""
    cleaned = _THINK_RE.sub("", raw).strip()
    m = _FENCE_RE.match(cleaned)
    if m:
        cleaned = m.group(1).strip()
    # Remove a trailing semicolon that LLMs routinely append to single SELECT statements.
    # This must happen before the _MULTI_STMT check so "SELECT ...;" is accepted while
    # "SELECT 1; SELECT 2" (semicolon followed by more SQL) is still rejected.
    cleaned = cleaned.rstrip().rstrip(";").rstrip()
    return cleaned


async def _nl_to_sql(question: str, settings: Settings, provider: LLMProvider) -> str:
    parts: list[str] = []
    async for delta in provider.chat_stream(
        model=settings.model_sql,
        messages=[
            ChatMessage(role="system", content=_SQL_SYSTEM_PROMPT),
            ChatMessage(role="user",   content=question),
        ],
        tools=None,
        temperature=0.0,
    ):
        if delta.text:
            parts.append(delta.text)
        if delta.finish_reason:
            break
    raw = "".join(parts)
    sql = _clean_model_output(raw)
    if not sql:
        raise ValueError("le modèle SQL n'a retourné aucune requête")
    return sql


# ── pyodbc execution ─────────────────────────────────────────────────────────

_PWD_RE = re.compile(r"(PWD|Password)=[^;]+", re.IGNORECASE)


def _safe_error(exc: Exception) -> str:
    """Return exc message with credentials scrubbed."""
    return _PWD_RE.sub(r"\1=***", str(exc))


def _run_query_sync(
    conn_str: str,
    sql: str,
    *,
    connection_factory=None,
    row_cap: int = 500,
) -> dict[str, Any]:
    """Synchronous execution — called via asyncio.to_thread."""
    # pyodbc imported only when no factory is injected (tests skip the import).
    if connection_factory is not None:
        factory = connection_factory
    else:
        import pyodbc  # lazy: keeps module importable without ODBC stack
        factory = pyodbc.connect
    try:
        with closing(factory(conn_str)) as conn:
            conn.timeout = 5  # query timeout in seconds
            cursor = conn.cursor()
            cursor.execute(sql)
            columns = [col[0] for col in cursor.description] if cursor.description else []
            fetched = cursor.fetchmany(row_cap + 1)
            capped = len(fetched) > row_cap
            rows = [list(row) for row in fetched[:row_cap]]
            # Coerce non-JSON-serializable types (Decimal → float, datetime → ISO str).
            from decimal import Decimal
            import datetime as _dt
            for row in rows:
                for i, v in enumerate(row):
                    if isinstance(v, (_dt.datetime, _dt.date)):
                        row[i] = v.isoformat()
                    elif isinstance(v, Decimal):
                        row[i] = float(v)
            return {"columns": columns, "rows": rows, "rowcount": len(rows), "capped": capped}
    except Exception as exc:
        raise RuntimeError(_safe_error(exc)) from exc


# ── public entry point ───────────────────────────────────────────────────────

async def execute_query_dw(
    question: str,
    settings: Settings,
    provider: LLMProvider,
    *,
    connection_factory=None,
) -> dict[str, Any]:
    """Full NL→SQL→DB pipeline. Always returns an {ok, ...} envelope."""
    if not settings.dw_readonly_conn:
        return {
            "ok": False,
            "error": "DW read-only connection non configurée (COPILOT_DW_READONLY_CONN)",
        }

    try:
        sql = await _nl_to_sql(question, settings, provider)
    except Exception as exc:
        return {"ok": False, "error": f"NL→SQL échoué: {exc}"}

    valid, reason = _validate_sql(sql)
    if not valid:
        return {"ok": False, "error": f"SQL rejeté (L4): {reason}", "hint": "Reformulez la question."}

    try:
        result = await asyncio.to_thread(
            _run_query_sync,
            settings.dw_readonly_conn,
            sql,
            connection_factory=connection_factory,
        )
    except Exception as exc:
        return {"ok": False, "error": f"Erreur DB: {exc}"}

    return {
        "ok": True,
        "data": {
            "sql":      sql,
            "columns":  result["columns"],
            "rows":     result["rows"],
            "rowcount": result["rowcount"],
            "capped":   result["capped"],
        },
    }
