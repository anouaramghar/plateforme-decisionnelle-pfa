"""Tests for the query_dw NL→SQL pipeline (no real DB, no real NIM key)."""
from __future__ import annotations

from typing import Any
from unittest.mock import MagicMock

import pytest

from config import Settings
from provider import MockLLMProvider


def _settings(conn: str | None = "DRIVER=fake;SERVER=db;DATABASE=PFA_DW;UID=x;PWD=secret;") -> Settings:
    return Settings(
        AGENT_INTERNAL_TOKEN="t",
        NVIDIA_NIM_API_KEY="k",
        COPILOT_DW_READONLY_CONN=conn,
    )


def _fake_factory(rows: list[list], columns: list[str]):
    """Returns a pyodbc.connect replacement that yields scripted rows."""
    def factory(conn_str: str):
        conn = MagicMock()
        cursor = MagicMock()
        cursor.description = [(col, None, None, None, None, None, None) for col in columns]
        cursor.fetchmany.return_value = rows
        conn.cursor.return_value = cursor
        conn.__enter__ = lambda s: s
        conn.__exit__ = MagicMock(return_value=False)
        conn.close = MagicMock()
        return conn
    return factory


# ── _validate_sql ────────────────────────────────────────────────────────────

def test_validate_sql_select_ok():
    from query_dw import _validate_sql
    ok, _ = _validate_sql("SELECT * FROM FaitNotes")
    assert ok is True


def test_validate_sql_select_with_join_ok():
    from query_dw import _validate_sql
    sql = "SELECT e.Matricule, AVG(f.NoteFinale) FROM FaitNotes f JOIN DimEtudiant e ON e.EtudiantKey=f.EtudiantKey GROUP BY e.Matricule"
    ok, _ = _validate_sql(sql)
    assert ok is True


def test_validate_sql_with_cte_ok():
    from query_dw import _validate_sql
    sql = "WITH cte AS (SELECT * FROM FaitNotes) SELECT * FROM cte"
    ok, _ = _validate_sql(sql)
    assert ok is True


def test_validate_sql_insert_rejected():
    from query_dw import _validate_sql
    ok, _ = _validate_sql("INSERT INTO FaitNotes VALUES (1,1,1,10.0,0,NULL,NULL)")
    assert ok is False  # fails SELECT-first check


def test_validate_sql_drop_rejected():
    from query_dw import _validate_sql
    ok, _ = _validate_sql("DROP TABLE DimEtudiant")
    assert ok is False  # fails SELECT-first check; blocklist also catches DROP


def test_validate_sql_update_rejected():
    from query_dw import _validate_sql
    ok, err = _validate_sql("UPDATE FaitNotes SET NoteFinale = 20.0")
    assert ok is False


def test_validate_sql_delete_rejected():
    from query_dw import _validate_sql
    ok, err = _validate_sql("DELETE FROM FaitNotes")
    assert ok is False


def test_validate_sql_exec_rejected():
    from query_dw import _validate_sql
    ok, err = _validate_sql("EXEC sp_executesql N'SELECT 1'")
    assert ok is False


def test_validate_sql_cross_db_rejected():
    from query_dw import _validate_sql
    ok, err = _validate_sql("SELECT * FROM PFA_DB.dbo.Etudiants")
    assert ok is False
    assert "PFA_DB" in err


def test_validate_sql_multistatement_rejected():
    from query_dw import _validate_sql
    ok, err = _validate_sql("SELECT 1; DROP TABLE DimEtudiant")
    assert ok is False


def test_validate_sql_trailing_semicolon_ok():
    from query_dw import _validate_sql
    ok, _ = _validate_sql("SELECT 1;")
    assert ok is True


def test_validate_sql_empty_rejected():
    from query_dw import _validate_sql
    for bad in ("", "   ", "\t\n"):
        ok, _ = _validate_sql(bad)
        assert ok is False, f"empty/whitespace should be rejected: {repr(bad)}"


def test_validate_sql_case_insensitive_drop():
    from query_dw import _validate_sql
    ok, _ = _validate_sql("select * from x; drop table y")
    assert ok is False


# ── _clean_model_output ───────────────────────────────────────────────────────

def test_clean_strips_markdown_fences():
    from query_dw import _clean_model_output
    raw = "```sql\nSELECT 1\n```"
    assert _clean_model_output(raw) == "SELECT 1"


def test_clean_strips_think_blocks():
    from query_dw import _clean_model_output
    raw = "<think>reasoning here</think>\nSELECT * FROM FaitNotes"
    assert _clean_model_output(raw) == "SELECT * FROM FaitNotes"


def test_clean_strips_both():
    from query_dw import _clean_model_output
    raw = "<think>blah</think>```sql\nSELECT 1\n```"
    assert _clean_model_output(raw) == "SELECT 1"


# ── execute_query_dw ─────────────────────────────────────────────────────────

@pytest.mark.asyncio
async def test_execute_query_dw_happy_path():
    from query_dw import execute_query_dw

    provider = MockLLMProvider(scripted_chunks=["SELECT COUNT(*) AS nb FROM DimEtudiant"])
    factory  = _fake_factory([[300]], ["nb"])

    result = await execute_query_dw(
        question="Combien d'étudiants ?",
        settings=_settings(),
        provider=provider,
        connection_factory=factory,
    )

    assert result["ok"] is True
    assert result["data"]["columns"] == ["nb"]
    assert result["data"]["rows"] == [[300]]
    assert result["data"]["rowcount"] == 1
    assert result["data"]["capped"] is False
    assert "SELECT" in result["data"]["sql"].upper()


@pytest.mark.asyncio
async def test_execute_query_dw_capped_flag():
    from query_dw import execute_query_dw

    provider = MockLLMProvider(scripted_chunks=["SELECT Matricule FROM DimEtudiant"])
    # Return 501 rows → should be capped to 500
    factory  = _fake_factory([[f"E{i:05d}"] for i in range(501)], ["Matricule"])

    result = await execute_query_dw(
        question="Tous les étudiants",
        settings=_settings(),
        provider=provider,
        connection_factory=factory,
    )

    assert result["ok"] is True
    assert result["data"]["capped"] is True
    assert result["data"]["rowcount"] == 500
    assert len(result["data"]["rows"]) == 500


@pytest.mark.asyncio
async def test_execute_query_dw_no_conn_string():
    from query_dw import execute_query_dw

    provider = MockLLMProvider(scripted_chunks=["SELECT 1"])

    result = await execute_query_dw(
        question="test",
        settings=_settings(conn=None),
        provider=provider,
    )

    assert result["ok"] is False
    assert "COPILOT_DW_READONLY_CONN" in result["error"]


@pytest.mark.asyncio
async def test_execute_query_dw_invalid_sql_from_model():
    from query_dw import execute_query_dw

    # Model returns a DROP statement — L4 must reject it
    provider = MockLLMProvider(scripted_chunks=["DROP TABLE DimEtudiant"])

    def should_not_be_called(conn_str):
        raise AssertionError("pyodbc must not be called when SQL is rejected by L4")

    result = await execute_query_dw(
        question="supprime les données",
        settings=_settings(),
        provider=provider,
        connection_factory=should_not_be_called,
    )

    assert result["ok"] is False
    assert "L4" in result["error"]


@pytest.mark.asyncio
async def test_execute_query_dw_db_error_does_not_propagate():
    from query_dw import execute_query_dw

    provider = MockLLMProvider(scripted_chunks=["SELECT 1"])

    def raise_error(conn_str):
        raise RuntimeError("connection refused")

    result = await execute_query_dw(
        question="test",
        settings=_settings(),
        provider=provider,
        connection_factory=raise_error,
    )

    assert result["ok"] is False
    assert "DB" in result["error"] or "connection" in result["error"]


@pytest.mark.asyncio
async def test_execute_query_dw_password_redacted_in_error():
    from query_dw import execute_query_dw

    provider = MockLLMProvider(scripted_chunks=["SELECT 1"])

    def raise_with_creds(conn_str):
        raise RuntimeError(f"Cannot connect: {conn_str}")

    result = await execute_query_dw(
        question="test",
        settings=_settings(conn="DRIVER=fake;UID=u;PWD=mysecret;SERVER=db;"),
        provider=provider,
        connection_factory=raise_with_creds,
    )

    assert result["ok"] is False
    assert "mysecret" not in result["error"]


@pytest.mark.asyncio
async def test_execute_query_dw_markdown_fences_stripped():
    from query_dw import execute_query_dw

    provider = MockLLMProvider(scripted_chunks=["```sql\nSELECT 1 AS x\n```"])
    factory  = _fake_factory([[1]], ["x"])

    result = await execute_query_dw(
        question="test",
        settings=_settings(),
        provider=provider,
        connection_factory=factory,
    )

    assert result["ok"] is True
    assert result["data"]["sql"] == "SELECT 1 AS x"
