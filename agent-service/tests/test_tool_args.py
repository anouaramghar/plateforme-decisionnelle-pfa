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


def test_unknown_tool_rejected():
    from tool_args import validate_tool_args

    ok, result = validate_tool_args("drop_table", {})
    assert ok is False
    assert "unknown tool" in result
