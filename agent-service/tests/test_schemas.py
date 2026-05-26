"""Pydantic schemas for /agent/run input + SSE event envelopes."""
from __future__ import annotations

import json

import pytest
from pydantic import ValidationError


def test_agent_run_request_minimal():
    from schemas import AgentRunRequest, ChatMessage, UserContext

    req = AgentRunRequest(
        trace_id="abc-123",
        user_ctx=UserContext(user_id=42, role="Responsable"),
        messages=[ChatMessage(role="user", content="Bonjour")],
    )

    assert req.trace_id == "abc-123"
    assert req.user_ctx.user_id == 42
    assert req.messages[0].content == "Bonjour"


def test_agent_run_request_rejects_unknown_role():
    from schemas import AgentRunRequest, ChatMessage, UserContext

    with pytest.raises(ValidationError):
        AgentRunRequest(
            trace_id="x",
            user_ctx=UserContext(user_id=1, role="Responsable"),
            messages=[ChatMessage(role="not-a-role", content="hi")],  # type: ignore[arg-type]
        )


def test_token_event_serializes_to_expected_sse_payload():
    from schemas import TokenEvent

    ev = TokenEvent(text="Hello")
    body = ev.model_dump()

    assert body == {"text": "Hello"}
    # Verify JSON round-trips cleanly — SSE wire format depends on this.
    assert json.loads(json.dumps(body)) == body


def test_done_event_carries_token_usage():
    from schemas import DoneEvent

    ev = DoneEvent(tokens_in=1234, tokens_out=567, latency_ms=8200)
    assert ev.model_dump() == {
        "tokens_in": 1234,
        "tokens_out": 567,
        "latency_ms": 8200,
    }
