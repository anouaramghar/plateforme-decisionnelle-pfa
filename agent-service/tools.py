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

LIST_AT_RISK_TOOL: dict[str, Any] = {
    "type": "function",
    "function": {
        "name": "list_at_risk",
        "description": (
            "Retourne la liste des étudiants dont le score de risque d'échec est "
            "supérieur ou égal au seuil donné. Résultats triés par risque décroissant, "
            "limités à 50 étudiants."
        ),
        "parameters": {
            "type": "object",
            "properties": {
                "threshold": {
                    "type": "number",
                    "minimum": 0.0,
                    "maximum": 1.0,
                    "description": (
                        "Seuil de score de risque (0.0–1.0). "
                        "0.35 = risque modéré+, 0.65 = risque élevé."
                    ),
                },
                "filiere": {
                    "type": "string",
                    "description": "Code filière (ex: 'GI', 'IRSI'). Optionnel.",
                },
                "niveau": {
                    "type": "string",
                    "description": "Niveau (ex: 'CI1', 'CI2'). Optionnel.",
                },
            },
            "required": ["threshold"],
            "additionalProperties": False,
        },
    },
}

QUERY_DW_TOOL: dict[str, Any] = {
    "type": "function",
    "function": {
        "name": "query_dw",
        "description": (
            "Interroge l'entrepôt de données PFA_DW par une question en langage naturel. "
            "Retourne les lignes correspondantes, la requête SQL générée et le nombre de lignes. "
            "Limité à 500 lignes, timeout 5 s. Seules les requêtes SELECT sont autorisées."
        ),
        "parameters": {
            "type": "object",
            "properties": {
                "question": {
                    "type": "string",
                    "description": (
                        "Question analytique en français sur les données DW. "
                        "Ex: 'Quels modules ont une note finale moyenne inférieure à 10 ?'"
                    ),
                },
            },
            "required": ["question"],
            "additionalProperties": False,
        },
    },
}

EXPLAIN_RISK_TOOL: dict[str, Any] = {
    "type": "function",
    "function": {
        "name": "explain_risk",
        "description": (
            "Explique les facteurs de risque d'échec d'un étudiant : "
            "décompose le score de risque en contributions individuelles "
            "(notes, absences, modules non validés, tendance) avec description en français."
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

DRAFT_ALERT_TOOL: dict[str, Any] = {
    "type": "function",
    "function": {
        "name": "draft_alert",
        "description": (
            "Prépare un brouillon d'alerte pour un étudiant. "
            "L'alerte N'EST PAS envoyée immédiatement — elle attend la confirmation "
            "explicite de l'utilisateur (bouton Confirmer). Expire après 5 minutes."
        ),
        "parameters": {
            "type": "object",
            "properties": {
                "matricule": {
                    "type": "string",
                    "description": "Matricule exact de l'étudiant, ex: 20231042",
                },
                "severity": {
                    "type": "string",
                    "enum": ["low", "medium", "high"],
                    "description": (
                        "Niveau de sévérité : low = surveillance, "
                        "medium = attention requise, high = risque élevé."
                    ),
                },
                "message_fr": {
                    "type": "string",
                    "description": "Message de l'alerte en français (max 1000 caractères).",
                },
            },
            "required": ["matricule", "severity", "message_fr"],
            "additionalProperties": False,
        },
    },
}

# Tools that require explicit user confirmation before taking effect (Tier-2 / L5).
TIER2_TOOLS: frozenset[str] = frozenset({"draft_alert"})

# name -> (spec, allowed roles)
TOOL_REGISTRY: dict[str, dict[str, Any]] = {
    "get_student":   {"spec": GET_STUDENT_TOOL,   "roles": {"Admin", "Responsable"}},
    "list_at_risk":  {"spec": LIST_AT_RISK_TOOL,  "roles": {"Admin", "Responsable"}},
    "query_dw":      {"spec": QUERY_DW_TOOL,      "roles": {"Admin", "Responsable"}},
    "explain_risk":  {"spec": EXPLAIN_RISK_TOOL,  "roles": {"Admin", "Responsable"}},
    "draft_alert":   {"spec": DRAFT_ALERT_TOOL,   "roles": {"Admin", "Responsable"}},
}


def tools_for_role(role: str) -> list[dict[str, Any]]:
    """Return the OpenAI tool specs the given role is allowed to call."""
    return [
        entry["spec"]
        for entry in TOOL_REGISTRY.values()
        if role in entry["roles"]
    ]
