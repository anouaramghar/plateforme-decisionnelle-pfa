"""L3 — per-tool argument validation."""
from __future__ import annotations


def test_valid_get_student_args():
    from tool_args import validate_tool_args

    ok, result = validate_tool_args("get_student", {"matricule": "20231042"})
    assert ok is True
    assert result == {"matricule": "20231042"}


def test_missing_required_arg_rejected():
    from tool_args import validate_tool_args

    ok, result = validate_tool_args("get_student", {})
    assert ok is False
    assert isinstance(result, str)  # an error message


def test_extra_arg_rejected():
    from tool_args import validate_tool_args

    ok, _ = validate_tool_args(
        "get_student", {"matricule": "x", "email": "a@b.ma"}
    )
    assert ok is False  # additionalProperties forbidden — no exfil via args


def test_oversized_matricule_rejected():
    from tool_args import validate_tool_args

    ok, _ = validate_tool_args("get_student", {"matricule": "x" * 50})
    assert ok is False


def test_valid_list_at_risk_args_minimal():
    from tool_args import validate_tool_args

    ok, result = validate_tool_args("list_at_risk", {"threshold": 0.5})
    assert ok is True
    assert result["threshold"] == 0.5


def test_valid_list_at_risk_args_full():
    from tool_args import validate_tool_args

    ok, result = validate_tool_args(
        "list_at_risk", {"threshold": 0.65, "filiere": "GI", "niveau": "CI1"}
    )
    assert ok is True
    assert result == {"threshold": 0.65, "filiere": "GI", "niveau": "CI1"}


def test_list_at_risk_threshold_out_of_range_rejected():
    from tool_args import validate_tool_args

    for bad in (-0.1, 1.1, 2.0):
        ok, _ = validate_tool_args("list_at_risk", {"threshold": bad})
        assert ok is False, f"expected rejection for threshold={bad}"


def test_list_at_risk_extra_arg_rejected():
    from tool_args import validate_tool_args

    ok, _ = validate_tool_args("list_at_risk", {"threshold": 0.5, "hack": "x"})
    assert ok is False


def test_list_at_risk_missing_threshold_rejected():
    from tool_args import validate_tool_args

    ok, _ = validate_tool_args("list_at_risk", {})
    assert ok is False


def test_unknown_tool_rejected():
    from tool_args import validate_tool_args

    ok, result = validate_tool_args("drop_table", {})
    assert ok is False
    assert "unknown tool" in result


# ── query_dw ─────────────────────────────────────────────────────────────────

def test_valid_query_dw_args():
    from tool_args import validate_tool_args

    ok, result = validate_tool_args("query_dw", {"question": "Combien d'étudiants ?"})
    assert ok is True
    assert result == {"question": "Combien d'étudiants ?"}


def test_query_dw_empty_question_rejected():
    from tool_args import validate_tool_args

    ok, _ = validate_tool_args("query_dw", {"question": ""})
    assert ok is False


def test_query_dw_oversized_question_rejected():
    from tool_args import validate_tool_args

    ok, _ = validate_tool_args("query_dw", {"question": "x" * 1001})
    assert ok is False


def test_query_dw_extra_arg_rejected():
    from tool_args import validate_tool_args

    ok, _ = validate_tool_args("query_dw", {"question": "test", "sql": "DROP TABLE x"})
    assert ok is False


def test_query_dw_missing_question_rejected():
    from tool_args import validate_tool_args

    ok, _ = validate_tool_args("query_dw", {})
    assert ok is False


# ── explain_risk ──────────────────────────────────────────────────────────────

def test_valid_explain_risk_args():
    from tool_args import validate_tool_args

    ok, result = validate_tool_args("explain_risk", {"matricule": "20231042"})
    assert ok is True
    assert result == {"matricule": "20231042"}


def test_explain_risk_empty_matricule_rejected():
    from tool_args import validate_tool_args

    ok, _ = validate_tool_args("explain_risk", {"matricule": ""})
    assert ok is False


def test_explain_risk_oversized_matricule_rejected():
    from tool_args import validate_tool_args

    ok, _ = validate_tool_args("explain_risk", {"matricule": "x" * 21})
    assert ok is False


def test_explain_risk_extra_arg_rejected():
    from tool_args import validate_tool_args

    ok, _ = validate_tool_args("explain_risk", {"matricule": "x", "email": "a@b"})
    assert ok is False


def test_explain_risk_missing_matricule_rejected():
    from tool_args import validate_tool_args

    ok, _ = validate_tool_args("explain_risk", {})
    assert ok is False


# ── draft_alert ───────────────────────────────────────────────────────────────

def test_valid_draft_alert_args_all_severities():
    from tool_args import validate_tool_args

    for sev in ("low", "medium", "high"):
        ok, result = validate_tool_args(
            "draft_alert",
            {"matricule": "E10001", "severity": sev, "message_fr": "Test message alerte."},
        )
        assert ok is True, f"should accept severity={sev}"
        assert result["severity"] == sev


def test_draft_alert_invalid_severity_rejected():
    from tool_args import validate_tool_args

    for bad in ("critical", "eleve", "Eleve", "HIGH"):
        ok, _ = validate_tool_args(
            "draft_alert",
            {"matricule": "E10001", "severity": bad, "message_fr": "x"},
        )
        assert ok is False, f"should reject severity={bad}"


def test_draft_alert_oversized_message_rejected():
    from tool_args import validate_tool_args

    ok, _ = validate_tool_args(
        "draft_alert",
        {"matricule": "E10001", "severity": "high", "message_fr": "x" * 1001},
    )
    assert ok is False


def test_draft_alert_empty_message_rejected():
    from tool_args import validate_tool_args

    ok, _ = validate_tool_args(
        "draft_alert",
        {"matricule": "E10001", "severity": "high", "message_fr": ""},
    )
    assert ok is False


def test_draft_alert_extra_arg_rejected():
    from tool_args import validate_tool_args

    ok, _ = validate_tool_args(
        "draft_alert",
        {"matricule": "E10001", "severity": "high", "message_fr": "x", "email": "a@b"},
    )
    assert ok is False


def test_draft_alert_missing_matricule_rejected():
    from tool_args import validate_tool_args

    ok, _ = validate_tool_args("draft_alert", {"severity": "high", "message_fr": "x"})
    assert ok is False


def test_draft_alert_missing_severity_rejected():
    from tool_args import validate_tool_args

    ok, _ = validate_tool_args("draft_alert", {"matricule": "E10001", "message_fr": "x"})
    assert ok is False


def test_draft_alert_missing_message_rejected():
    from tool_args import validate_tool_args

    ok, _ = validate_tool_args("draft_alert", {"matricule": "E10001", "severity": "high"})
    assert ok is False


def test_draft_alert_oversized_matricule_rejected():
    from tool_args import validate_tool_args

    ok, _ = validate_tool_args(
        "draft_alert",
        {"matricule": "x" * 21, "severity": "low", "message_fr": "x"},
    )
    assert ok is False
