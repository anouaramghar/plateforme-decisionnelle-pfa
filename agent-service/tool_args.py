"""
L3 — tool argument validation. One pydantic model per tool. extra='forbid'
enforces the "no free-text recipients / no exfil via args" rule from spec §3.3:
the model rejects any field the schema didn't declare.
"""
from __future__ import annotations

from typing import Any

from pydantic import BaseModel, Field, ValidationError


class GetStudentArgs(BaseModel):
    model_config = {"extra": "forbid"}
    matricule: str = Field(min_length=1, max_length=20)


# name -> pydantic model
TOOL_ARG_MODELS: dict[str, type[BaseModel]] = {
    "get_student": GetStudentArgs,
}


def validate_tool_args(
    name: str, raw: dict[str, Any]
) -> tuple[bool, dict[str, Any] | str]:
    """
    Validate raw LLM-supplied args against the tool's model.

    Returns (True, cleaned_args_dict) on success, or (False, error_message) on
    failure — the loop relays the message back to the LLM so it can retry.
    """
    model = TOOL_ARG_MODELS.get(name)
    if model is None:
        return False, f"unknown tool: {name}"
    try:
        parsed = model(**raw)
    except ValidationError as exc:
        return False, f"invalid args for {name}: {exc.errors()[0]['msg']}"
    except TypeError as exc:  # raw not a mapping, etc.
        return False, f"invalid args for {name}: {exc}"
    return True, parsed.model_dump()
