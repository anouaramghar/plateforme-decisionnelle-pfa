"""Agent loop — chat-only run (no tools yet)."""
from __future__ import annotations

import pytest

from provider import MockLLMProvider
from schemas import (
    AgentRunRequest, ChatMessage, DoneEvent, TokenEvent, UserContext,
)


@pytest.mark.asyncio
async def test_agent_run_yields_token_then_done_with_mock():
    from agent import run_stream

    req = AgentRunRequest(
        trace_id="t1",
        user_ctx=UserContext(user_id=1, role="Responsable"),
        messages=[ChatMessage(role="user", content="Bonjour")],
    )
    provider = MockLLMProvider(
        scripted_chunks=["Bon", "jour", "!"], tokens_in=5, tokens_out=3
    )

    events: list[tuple[str, dict]] = []
    async for name, payload in run_stream(req, provider=provider, model="any"):
        events.append((name, payload))

    # Expected sequence: 3 token events, then 1 done event.
    names = [n for n, _ in events]
    assert names == ["token", "token", "token", "done"]

    payloads = [p for _, p in events]
    assert TokenEvent(**payloads[0]).text == "Bon"
    assert TokenEvent(**payloads[1]).text == "jour"
    assert TokenEvent(**payloads[2]).text == "!"
    done = DoneEvent(**payloads[3])
    assert done.tokens_in == 5
    assert done.tokens_out == 3
    assert done.latency_ms >= 0
