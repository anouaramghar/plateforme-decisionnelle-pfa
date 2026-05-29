"""
Tool catalog advertised to the LLM (OpenAI function-calling schema).

P1.B slice: one Tier-1 read tool — get_student. Role filtering is real even
with one tool: agent-service only advertises tools the caller's role may use,
so the LLM is never tempted by capabilities it cannot exercise (spec §3.2).
"""
from __future__ import annotations

from typing import Any

GET_STUDENT_TOOL: dict[str, Any] = {
    "type": "function",
    "function": {
        "name": "get_student",
        "description": (
            "Récupère le profil d'un étudiant par son matricule exact : "
            "filière, niveau, moyenne générale, total d'absences (heures) et "
            "niveau de risque d'échec."
        ),
        "parameters": {
            "type": "object",
            "properties": {
                "matricule": {
                    "type": "string",
                    "description": "Matricule exact de l'étudiant, ex: 20231042",
                },
            },
            "required": ["matricule"],
            "additionalProperties": False,
        },
    },
}

# name -> (spec, allowed roles)
TOOL_REGISTRY: dict[str, dict[str, Any]] = {
    "get_student": {"spec": GET_STUDENT_TOOL, "roles": {"Admin", "Responsable"}},
}


def tools_for_role(role: str) -> list[dict[str, Any]]:
    """Return the OpenAI tool specs the given role is allowed to call."""
    return [
        entry["spec"]
        for entry in TOOL_REGISTRY.values()
        if role in entry["roles"]
    ]
