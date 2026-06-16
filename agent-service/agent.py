"""
ENIAD Copilot agent loop (P1.B).

Bounded tool-calling loop:
  1. Advertise role-filtered tools to the model.
  2. Stream assistant tokens.
  3. On finish_reason == "tool_calls": for each call, validate args (L3),
     execute via the backend callback, feed the result back, and loop.
  4. On finish_reason == "stop": emit done.
  5. Iteration cap (L6) -> emit error if the model never stops.

Safety layers L1 (input guard), L4 (SQL), L5 (confirm), L7 (output guard),
and L8 (audit) are later P1.B slices.
"""
from __future__ import annotations

import json
import time
from collections.abc import AsyncIterator, Awaitable, Callable
from typing import Any

from config import Settings
from provider import LLMProvider
from schemas import (
    AgentRunRequest, ChatMessage, ConfirmRequestEvent, DoneEvent, ErrorEvent,
    ToolCallEvent, ToolResultEvent, TokenEvent,
)
from query_dw import execute_query_dw
from tool_args import validate_tool_args
from tools import TIER2_TOOLS, tools_for_role

# (name, args, *, jwt) -> tool result envelope dict
ToolCaller = Callable[..., Awaitable[dict[str, Any]]]

_SYSTEM_PROMPT_FR = (
    "Tu es ENIAD Copilot, un assistant d'aide à la décision pour le personnel "
    "pédagogique de l'ENIAD (École Nationale d'Ingénieurs et d'Architectes de "
    "Droits, Berkane, Maroc). Tu dois :\n"
    "1. Répondre en français, de façon concise et factuelle.\n"
    "2. N'appeler les outils QUE lorsque l'utilisateur demande des données "
    "spécifiques sur un étudiant, une filière, ou l'entrepôt de données. "
    "Pour une salutation, un remerciement ou une question générale (ex. 'Bonjour', "
    "'Merci', 'Que peux-tu faire ?'), réponds DIRECTEMENT sans utiliser d'outils.\n"
    "3. Utiliser les outils pour récupérer des données réelles — ne jamais inventer "
    "de matricules, de scores ou de chiffres.\n"
    "4. Lorsque tu appelles un outil, utilise les valeurs fournies par l'utilisateur "
    "dans sa question. Ne jamais utiliser des valeurs d'exemple comme "
    "'20231042' ou 'E10001' à moins que l'utilisateur ne les ait mentionnées.\n"
    "5. Si un outil renvoie une erreur, expliquer brièvement et proposer une "
    "alternative (ex. vérifier le matricule)."
)


def _build_system_prompt(req: AgentRunRequest) -> str:
    prompt = _SYSTEM_PROMPT_FR
    pc = req.user_ctx.page_context
    if pc:
        prompt += f"\n\nContexte de la page courante: {json.dumps(pc, ensure_ascii=False)}"
    return prompt


def _summarize(result: dict[str, Any]) -> str:
    """Short human label for the tool_result SSE chip."""
    if result.get("ok"):
        return "ok"
    return str(result.get("error", "error"))[:200]


async def run_stream(
    req: AgentRunRequest,
    *,
    provider: LLMProvider,
    settings: Settings,
    tool_caller: ToolCaller,
) -> AsyncIterator[tuple[str, dict[str, Any]]]:
    started = time.monotonic()
    tokens_in = 0   # accumulated across all LLM calls in this turn
    tokens_out = 0

    # Working message list seeded with system prompt + the inbound history.
    messages: list[ChatMessage] = [
        ChatMessage(role="system", content=_build_system_prompt(req)),
        *req.messages,
    ]
    tools = tools_for_role(req.user_ctx.role)
    jwt = req.user_ctx.jwt

    try:
        for _ in range(settings.max_tool_iterations):
            assistant_text = ""
            pending_calls: list[dict[str, Any]] = []
            finish_reason: str | None = None

            async for delta in provider.chat_stream(
                model=settings.model_router, messages=messages, tools=tools,
            ):
                if delta.text:
                    assistant_text += delta.text
                    yield "token", TokenEvent(text=delta.text).model_dump()
                if delta.tool_call:
                    pending_calls.append(delta.tool_call)
                if delta.finish_reason:
                    finish_reason = delta.finish_reason

            tokens_in += getattr(provider, "last_tokens_in", 0) or 0
            tokens_out += getattr(provider, "last_tokens_out", 0) or 0

            if finish_reason != "tool_calls" or not pending_calls:
                # Model produced a final answer.
                yield "done", DoneEvent(
                    tokens_in=tokens_in, tokens_out=tokens_out,
                    latency_ms=int((time.monotonic() - started) * 1000),
                ).model_dump()
                return

            # Record the assistant turn that requested the tools (OpenAI shape).
            messages.append(ChatMessage(
                role="assistant",
                content=assistant_text or None,
                tool_calls=[
                    {
                        "id": c["id"],
                        "type": "function",
                        "function": {"name": c["name"], "arguments": c["arguments"]},
                    }
                    for c in pending_calls
                ],
            ))

            # Execute each requested tool, append its result as a tool message.
            for call in pending_calls:
                name = call["name"]
                call_id = call["id"]

                try:
                    raw_args = json.loads(call["arguments"] or "{}")
                    if not isinstance(raw_args, dict):
                        raise ValueError
                    json_ok = True
                except (json.JSONDecodeError, ValueError):
                    raw_args, json_ok = {}, False

                # Surface the requested call WITH its args before executing, so
                # the UI chip can show what was called (spec §4.2).
                yield "tool_call", ToolCallEvent(
                    name=name, args=raw_args, call_id=call_id,
                ).model_dump()

                if not json_ok:
                    result = {"ok": False, "error": "arguments were not valid JSON"}
                else:
                    valid, payload = validate_tool_args(name, raw_args)
                    if not valid:
                        result = {"ok": False, "error": payload}
                    elif name == "query_dw":
                        # query_dw executes locally in agent-service (no backend
                        # HTTP call) — direct pyodbc read against PFA_DW via the
                        # pfa_app_readonly login.
                        result = await execute_query_dw(
                            question=payload["question"],
                            settings=settings,
                            provider=provider,
                        )
                    else:
                        result = await tool_caller(name, payload, jwt=jwt)

                yield "tool_result", ToolResultEvent(
                    name=name, call_id=call_id,
                    ok=bool(result.get("ok")), summary=_summarize(result),
                    error=None if result.get("ok") else str(result.get("error", "")),
                ).model_dump()

                # Emit confirm_request for Tier-2 tools that succeeded (L5 gate).
                # The browser renders a confirm card; the actual write only happens
                # when the user POSTs /api/copilot/confirm.
                if name in TIER2_TOOLS and result.get("ok"):
                    draft_id = result.get("data", {}).get("draft_id")
                    if draft_id is not None:
                        yield "confirm_request", ConfirmRequestEvent(
                            draft_id=draft_id,
                            preview=result.get("data", {}),
                            tool=name,
                        ).model_dump()

                messages.append(ChatMessage(
                    role="tool",
                    tool_call_id=call_id,
                    name=name,
                    content=json.dumps(result, ensure_ascii=False),
                ))
            # loop back to the model with the tool results appended

        # Fell out of the loop without a final answer -> L6 bound hit.
        yield "error", ErrorEvent(
            message="Limite d'itérations atteinte (COPILOT_MAX_TOOL_ITERATIONS).",
        ).model_dump()

    except Exception as exc:  # noqa: BLE001 — surface as an SSE error event
        yield "error", ErrorEvent(message=f"{type(exc).__name__}: {exc}").model_dump()
