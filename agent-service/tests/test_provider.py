"""Provider interface + Mock implementation (no network) + NIM live test."""
from __future__ import annotations

import os

import pytest

from schemas import ChatMessage


@pytest.mark.asyncio
async def test_mock_provider_chat_stream_yields_scripted_chunks():
    from provider import MockLLMProvider

    scripted = ["Bon", "jour", "."]
    p = MockLLMProvider(scripted_chunks=scripted)

    chunks: list[str] = []
    async for delta in p.chat_stream(
        model="any",
        messages=[ChatMessage(role="user", content="hi")],
    ):
        chunks.append(delta.text or "")

    assert "".join(chunks) == "Bonjour."


@pytest.mark.asyncio
async def test_mock_provider_reports_usage():
    from provider import MockLLMProvider

    p = MockLLMProvider(scripted_chunks=["abc"], tokens_in=11, tokens_out=22)
    deltas = []
    async for delta in p.chat_stream(
        model="any",
        messages=[ChatMessage(role="user", content="x")],
    ):
        deltas.append(delta)
    assert any(d.usage_in == 11 for d in deltas)
    assert any(d.usage_out == 22 for d in deltas)


@pytest.mark.asyncio
@pytest.mark.skipif(
    not os.environ.get("NVIDIA_NIM_API_KEY"),
    reason="NVIDIA_NIM_API_KEY not set — skipping live NIM integration test",
)
async def test_nim_provider_real_round_trip():
    """Smoke test against the real NIM free tier — short prompt, cheap."""
    from provider import NIMProvider

    p = NIMProvider(
        api_key=os.environ["NVIDIA_NIM_API_KEY"],
        base_url="https://integrate.api.nvidia.com/v1",
    )

    full_text = ""
    finish: str | None = None
    async for delta in p.chat_stream(
        model="meta/llama-3.3-70b-instruct",
        messages=[ChatMessage(role="user", content="Reponds par un seul mot bonjour")],
        max_tokens=10,
    ):
        if delta.text:
            full_text += delta.text
        if delta.finish_reason:
            finish = delta.finish_reason

    assert finish in {"stop", "length"}
    assert len(full_text) > 0


@pytest.mark.asyncio
async def test_scripted_provider_replays_turns_in_order():
    from provider import ScriptedLLMProvider

    p = ScriptedLLMProvider(turns=[
        {"tool_call": {"id": "c1", "name": "get_student",
                       "arguments": '{"matricule": "42"}'}},
        {"text": ["Voici ", "le résumé."]},
    ])

    # First call -> a tool_call delta then finish 'tool_calls'.
    first = [d async for d in p.chat_stream(model="m", messages=[])]
    assert first[0].tool_call == {"id": "c1", "name": "get_student",
                                  "arguments": '{"matricule": "42"}'}
    assert first[-1].finish_reason == "tool_calls"

    # Second call -> text deltas then finish 'stop'.
    second = [d async for d in p.chat_stream(model="m", messages=[])]
    assert "".join(d.text or "" for d in second) == "Voici le résumé."
    assert second[-1].finish_reason == "stop"
