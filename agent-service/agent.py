"""
ENIAD Copilot agent loop.

P1.A scope: chat-only — proxies one streamed LLM call, emits token + done
events. Tools, safety layers, multi-iteration loop come in P1.B.
"""
from __future__ import annotations

import time
from collections.abc import AsyncIterator
from typing import Any

from provider import LLMProvider
from schemas import (
    AgentRunRequest, DoneEvent, ErrorEvent, TokenEvent,
)


async def run_stream(
    req: AgentRunRequest,
    *,
    provider: LLMProvider,
    model: str,
) -> AsyncIterator[tuple[str, dict[str, Any]]]:
    """
    Yield (event_name, payload_dict) tuples for /agent/run to encode as SSE.

    P1.A: passes the input messages straight to the provider, streams tokens,
    emits done. No tools, no safety guards.
    """
    started = time.monotonic()
    tokens_in = 0
    tokens_out = 0

    try:
        async for delta in provider.chat_stream(
            model=model, messages=req.messages,
        ):
            if delta.text:
                yield "token", TokenEvent(text=delta.text).model_dump()
            if delta.finish_reason:
                # Mock provider exposes last_tokens_*; real NIM doesn't surface
                # usage on streaming responses, so we leave it 0 for P1.A and
                # add a usage call in P1.B.
                tokens_in = getattr(provider, "last_tokens_in", 0) or 0
                tokens_out = getattr(provider, "last_tokens_out", 0) or 0
                break

        latency_ms = int((time.monotonic() - started) * 1000)
        yield "done", DoneEvent(
            tokens_in=tokens_in,
            tokens_out=tokens_out,
            latency_ms=latency_ms,
        ).model_dump()

    except Exception as exc:  # noqa: BLE001 — bubble up as an SSE error event
        yield "error", ErrorEvent(message=f"{type(exc).__name__}: {exc}").model_dump()
