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
