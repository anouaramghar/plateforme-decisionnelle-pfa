#!/usr/bin/env python3
"""
NIM Smoke Test — validates model + tool-calling assumptions before
committing to the ENIAD Copilot agent-service design.

Scenarios (each run 3x at temperature=0 for determinism):
  T1  Tier-1 router        meta/llama-3.3-70b-instruct
  T2  NL->SQL on PFA_DW    qwen/qwen2.5-coder-32b-instruct
  T3  Multi-step read->write  nvidia/llama-3.1-nemotron-70b-instruct

Run:
    # PowerShell
    $env:NVIDIA_NIM_API_KEY = "nvapi-..."
    pip install openai
    python scripts/nim_smoke_test.py

Cost: 0 USD (NIM free tier). Time: ~30-60s.
"""
from __future__ import annotations

import json
import os
import sys
import time
from dataclasses import dataclass, field
from typing import Any, Callable

try:
    from openai import OpenAI
except ImportError:
    sys.stderr.write("ERROR: 'openai' package missing. Run: pip install openai\n")
    sys.exit(1)


NIM_BASE_URL = "https://integrate.api.nvidia.com/v1"
RUNS_PER_TEST = 3

# ---------------------------------------------------------------
# Real PFA_DW schema (kept tight to fit small context budgets).
# Mirrors database/init_dw.sql.
# ---------------------------------------------------------------
DW_SCHEMA = """
PFA_DW (Microsoft SQL Server, star schema, READ-ONLY):

DimEtudiant (
  EtudiantKey INT PK, Matricule NVARCHAR(20),
  Nom NVARCHAR(100), Prenom NVARCHAR(100),
  Filiere NVARCHAR(100), Niveau NVARCHAR(10)
)
DimModule (
  ModuleKey INT PK, Code NVARCHAR(20),
  Nom NVARCHAR(200), Filiere NVARCHAR(100),
  Coefficient DECIMAL(4,2)
)
DimTemps (
  TempsKey INT PK, Annee NVARCHAR(9), Semestre NVARCHAR(5)
)
FaitNotes (
  Id INT PK,
  EtudiantKey INT FK -> DimEtudiant,
  ModuleKey INT FK -> DimModule,
  TempsKey INT FK -> DimTemps,
  NoteFinale DECIMAL(5,2),
  NbAbsences INT,
  ScoreRisque DECIMAL(5,4),
  Cluster INT
)
""".strip()


# ---------------------------------------------------------------
@dataclass
class TestResult:
    name: str
    model: str
    runs: int
    passes: int = 0
    failures: list[str] = field(default_factory=list)
    latencies_ms: list[int] = field(default_factory=list)

    @property
    def ok(self) -> bool:
        return self.passes == self.runs

    @property
    def avg_ms(self) -> int:
        return int(sum(self.latencies_ms) / len(self.latencies_ms)) if self.latencies_ms else 0


def run_scenario(
    client: OpenAI,
    name: str,
    model: str,
    build_request: Callable[[], dict[str, Any]],
    verify: Callable[[Any], tuple[bool, str]],
) -> TestResult:
    result = TestResult(name=name, model=model, runs=RUNS_PER_TEST)
    print(f"\n-- {name}  [{model}] --")
    for i in range(RUNS_PER_TEST):
        try:
            t0 = time.time()
            resp = client.chat.completions.create(**build_request())
            ms = int((time.time() - t0) * 1000)
            result.latencies_ms.append(ms)
            ok, reason = verify(resp)
            mark = "OK " if ok else "FAIL"
            print(f"   run {i + 1}: [{mark}] {ms:>5} ms  {reason}")
            if ok:
                result.passes += 1
            else:
                result.failures.append(f"run {i + 1}: {reason}")
        except Exception as e:
            print(f"   run {i + 1}: [FAIL]       --  EXCEPTION {type(e).__name__}: {e}")
            result.failures.append(f"run {i + 1}: {type(e).__name__}: {e}")
    return result


# ---------------------------------------------------------------
# T1: Tier-1 router  -- Llama 3.3 70B must emit get_student tool_call
# ---------------------------------------------------------------
def t1_request() -> dict[str, Any]:
    return {
        "model": "meta/llama-3.3-70b-instruct",
        "temperature": 0,
        "messages": [
            {
                "role": "system",
                "content": (
                    "Tu es ENIAD Copilot. Utilise les outils disponibles pour repondre. "
                    "N'invente jamais d'informations sur les etudiants."
                ),
            },
            {
                "role": "user",
                "content": "Donne-moi les informations de l'etudiant matricule 20231042.",
            },
        ],
        "tools": [
            {
                "type": "function",
                "function": {
                    "name": "get_student",
                    "description": "Retourne le profil complet d'un etudiant a partir de son matricule.",
                    "parameters": {
                        "type": "object",
                        "properties": {
                            "matricule": {
                                "type": "string",
                                "description": "Matricule unique de l'etudiant (ex: 20231042)",
                            },
                        },
                        "required": ["matricule"],
                    },
                },
            }
        ],
        "tool_choice": "auto",
    }


def t1_verify(resp: Any) -> tuple[bool, str]:
    msg = resp.choices[0].message
    calls = getattr(msg, "tool_calls", None) or []
    if not calls:
        snippet = (msg.content or "")[:80]
        return False, f"no tool_call (content starts: {snippet!r})"
    call = calls[0]
    if call.function.name != "get_student":
        return False, f"wrong tool: {call.function.name}"
    try:
        args = json.loads(call.function.arguments)
    except json.JSONDecodeError as e:
        return False, f"invalid JSON args: {e}"
    if str(args.get("matricule")) != "20231042":
        return False, f"wrong matricule: {args}"
    return True, "get_student(matricule=20231042)"


# ---------------------------------------------------------------
# T2: NL->SQL  -- Qwen Coder must emit safe SELECT on PFA_DW
# ---------------------------------------------------------------
def t2_request() -> dict[str, Any]:
    return {
        "model": "minimaxai/minimax-m2.7",
        "temperature": 0,
        "messages": [
            {
                "role": "system",
                "content": (
                    "Tu es un expert SQL Server. Genere UNIQUEMENT une requete SELECT "
                    "valide sur la base PFA_DW ci-dessous. "
                    "Aucun INSERT/UPDATE/DELETE/DROP/ALTER/TRUNCATE/EXEC autorise. "
                    "Reponds avec le SQL seul (pas d'explication, pas de markdown).\n\n"
                    f"{DW_SCHEMA}"
                ),
            },
            {
                "role": "user",
                "content": (
                    "Liste les etudiants de la filiere 'Genie Informatique' ayant une "
                    "moyenne (AVG de NoteFinale) inferieure a 10 au semestre S3 de "
                    "l'annee 2025/2026. Affiche Matricule, Nom, Prenom, moyenne."
                ),
            },
        ],
    }


def t2_verify(resp: Any) -> tuple[bool, str]:
    sql = (resp.choices[0].message.content or "").strip()
    # Strip markdown code fences if the model added them anyway.
    if sql.startswith("```"):
        sql = "\n".join(
            line for line in sql.splitlines() if not line.startswith("```")
        ).strip()
    upper = sql.upper()
    head = upper.lstrip()[:10]
    if not (head.startswith("SELECT") or head.startswith("WITH")):
        return False, f"not SELECT/WITH: starts {sql[:40]!r}"
    forbidden = ["INSERT ", "UPDATE ", "DELETE ", "DROP ", "ALTER ", "TRUNCATE ", "EXEC ", "MERGE "]
    for f in forbidden:
        if f in upper:
            return False, f"forbidden keyword: {f.strip()}"
    must_have = ["FAITNOTES", "DIMETUDIANT", "DIMTEMPS"]
    missing = [m for m in must_have if m not in upper]
    if missing:
        return False, f"missing tables: {missing}"
    # filiere filter (accept with/without accent)
    if "INFORMATIQUE" not in upper:
        return False, "filiere filter missing (no 'Informatique')"
    if "'S3'" not in sql and '"S3"' not in sql and "S3" not in upper:
        return False, "semester filter missing"
    if "2025/2026" not in sql and "2025" not in sql:
        return False, "year filter missing"
    return True, f"valid SQL ({len(sql)} chars)"


# ---------------------------------------------------------------
# T3: Multi-step read->write -- Nemotron must call list_at_risk FIRST
# (i.e. must NOT hallucinate student_ids by jumping to draft_alert)
# ---------------------------------------------------------------
def t3_request() -> dict[str, Any]:
    return {
        "model": "nvidia/llama-3.3-nemotron-super-49b-v1.5",
        "temperature": 0,
        "messages": [
            {
                "role": "system",
                "content": (
                    "Tu es ENIAD Copilot. Pour preparer des alertes, tu DOIS d'abord "
                    "lister les etudiants concernes via list_at_risk, PUIS appeler "
                    "draft_alert pour chacun avec les vraies donnees. "
                    "Ne JAMAIS inventer un student_id."
                ),
            },
            {
                "role": "user",
                "content": (
                    "Identifie tous les etudiants avec un score de risque > 0.8 "
                    "et prepare une alerte de severite 'high' pour chacun."
                ),
            },
        ],
        "tools": [
            {
                "type": "function",
                "function": {
                    "name": "list_at_risk",
                    "description": "Retourne les etudiants dont le score de risque depasse un seuil.",
                    "parameters": {
                        "type": "object",
                        "properties": {
                            "threshold": {"type": "number", "description": "Seuil entre 0 et 1"},
                        },
                        "required": ["threshold"],
                    },
                },
            },
            {
                "type": "function",
                "function": {
                    "name": "draft_alert",
                    "description": "Prepare (sans envoyer) une alerte pour UN etudiant.",
                    "parameters": {
                        "type": "object",
                        "properties": {
                            "student_id": {"type": "integer"},
                            "severity": {"type": "string", "enum": ["low", "medium", "high"]},
                            "message": {"type": "string"},
                        },
                        "required": ["student_id", "severity", "message"],
                    },
                },
            },
        ],
        "tool_choice": "auto",
    }


def t3_verify(resp: Any) -> tuple[bool, str]:
    msg = resp.choices[0].message
    calls = getattr(msg, "tool_calls", None) or []
    if not calls:
        snippet = (msg.content or "")[:80]
        return False, f"no tool_call (content starts: {snippet!r})"
    first = calls[0].function.name
    if first == "draft_alert":
        return False, "HALLUCINATION: jumped to draft_alert without read step"
    if first != "list_at_risk":
        return False, f"unexpected first tool: {first}"
    try:
        args = json.loads(calls[0].function.arguments)
    except json.JSONDecodeError as e:
        return False, f"invalid JSON args: {e}"
    thr = args.get("threshold")
    if not isinstance(thr, (int, float)) or not (0.75 <= float(thr) <= 0.85):
        return False, f"threshold off (expected ~0.8, got {thr})"
    return True, f"list_at_risk(threshold={thr})"


# ---------------------------------------------------------------
def _read_dotenv_key(repo_root: str, name: str) -> str | None:
    env_path = os.path.join(repo_root, ".env")
    if not os.path.exists(env_path):
        return None
    try:
        with open(env_path, encoding="utf-8") as fh:
            for line in fh:
                line = line.strip()
                if line.startswith(f"{name}="):
                    return line.split("=", 1)[1].strip().strip('"').strip("'")
    except OSError:
        return None
    return None


def main() -> int:
    repo_root = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
    api_key = os.environ.get("NVIDIA_NIM_API_KEY") or _read_dotenv_key(repo_root, "NVIDIA_NIM_API_KEY")
    if not api_key:
        sys.stderr.write(
            "ERROR: NVIDIA_NIM_API_KEY not set.\n"
            '  PowerShell:  $env:NVIDIA_NIM_API_KEY = "nvapi-..."\n'
            "  or add       NVIDIA_NIM_API_KEY=nvapi-...   to .env\n"
        )
        return 2

    client = OpenAI(base_url=NIM_BASE_URL, api_key=api_key)

    results = [
        run_scenario(
            client,
            "T1 Tier-1 router  (get_student)",
            "meta/llama-3.3-70b-instruct",
            t1_request,
            t1_verify,
        ),
        run_scenario(
            client,
            "T2 NL->SQL         (DW query)",
            "minimaxai/minimax-m2.7",
            t2_request,
            t2_verify,
        ),
        run_scenario(
            client,
            "T3 Multi-step      (read->write)",
            "nvidia/llama-3.3-nemotron-super-49b-v1.5",
            t3_request,
            t3_verify,
        ),
    ]

    print()
    print("=" * 72)
    print("SUMMARY")
    print("=" * 72)
    all_ok = True
    for r in results:
        status = "PASS" if r.ok else "FAIL"
        print(
            f"  [{status}]  {r.name:<36}  {r.passes}/{r.runs}  "
            f"avg {r.avg_ms} ms   [{r.model}]"
        )
        if not r.ok:
            all_ok = False
            for f in r.failures:
                print(f"          - {f}")

    print()
    if all_ok:
        print("All models cleared the smoke test. Safe to lock the design.")
        return 0
    print("One or more models failed. Review failures above; consider swapping")
    print("the model in the design before committing.")
    return 1


if __name__ == "__main__":
    sys.exit(main())
