"""Tool registry + role filtering."""
from __future__ import annotations


def test_get_student_tool_is_advertised_to_staff_roles():
    from tools import tools_for_role

    for role in ("Admin", "Responsable"):
        specs = tools_for_role(role)
        names = [s["function"]["name"] for s in specs]
        assert "get_student" in names


def test_unknown_role_gets_no_tools():
    from tools import tools_for_role

    assert tools_for_role("Etudiant") == []


def test_list_at_risk_tool_is_advertised_to_staff_roles():
    from tools import tools_for_role

    for role in ("Admin", "Responsable"):
        names = [s["function"]["name"] for s in tools_for_role(role)]
        assert "list_at_risk" in names


def test_list_at_risk_schema_shape():
    from tools import tools_for_role

    spec = next(
        s for s in tools_for_role("Admin")
        if s["function"]["name"] == "list_at_risk"
    )
    params = spec["function"]["parameters"]
    assert "threshold" in params["required"]
    assert params["properties"]["threshold"]["type"] == "number"
    assert "filiere" in params["properties"]
    assert "niveau" in params["properties"]
    assert params["additionalProperties"] is False


def test_get_student_schema_shape():
    from tools import tools_for_role

    spec = next(
        s for s in tools_for_role("Admin")
        if s["function"]["name"] == "get_student"
    )
    assert spec["type"] == "function"
    params = spec["function"]["parameters"]
    assert params["required"] == ["matricule"]
    assert params["properties"]["matricule"]["type"] == "string"
    assert params["additionalProperties"] is False


def test_explain_risk_tool_is_advertised_to_staff_roles():
    from tools import tools_for_role

    for role in ("Admin", "Responsable"):
        names = [s["function"]["name"] for s in tools_for_role(role)]
        assert "explain_risk" in names


def test_explain_risk_schema_shape():
    from tools import tools_for_role

    spec = next(
        s for s in tools_for_role("Admin")
        if s["function"]["name"] == "explain_risk"
    )
    params = spec["function"]["parameters"]
    assert params["required"] == ["matricule"]
    assert params["properties"]["matricule"]["type"] == "string"
    assert params["additionalProperties"] is False


def test_draft_alert_tool_is_advertised_to_staff_roles():
    from tools import tools_for_role

    for role in ("Admin", "Responsable"):
        names = [s["function"]["name"] for s in tools_for_role(role)]
        assert "draft_alert" in names


def test_draft_alert_schema_shape():
    from tools import tools_for_role

    spec = next(
        s for s in tools_for_role("Admin")
        if s["function"]["name"] == "draft_alert"
    )
    params = spec["function"]["parameters"]
    assert set(params["required"]) == {"matricule", "severity", "message_fr"}
    assert params["properties"]["severity"]["enum"] == ["low", "medium", "high"]
    assert params["additionalProperties"] is False


def test_tier2_tools_contains_draft_alert():
    from tools import TIER2_TOOLS

    assert "draft_alert" in TIER2_TOOLS
    assert "explain_risk" not in TIER2_TOOLS
