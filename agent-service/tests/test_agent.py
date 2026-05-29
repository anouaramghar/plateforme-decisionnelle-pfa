"""Agent loop — chat-only and tool-calling runs (MockLLMProvider / Scripted)."""
from __future__ import annotations

import pytest

from config import Settings
from provider import MockLLMProvider, ScriptedLLMProvider
from schemas import AgentRunRequest, ChatMessage, UserContext


def _settings(max_iters: int = 8) -> Settings:
    # Minimal valid Settings for the loop (router model + iteration bound).
    return Settings(
        AGENT_INTERNAL_TOKEN="t",
        NVIDIA_NIM_API_KEY="k",
        COPILOT_MAX_TOOL_ITERATIONS=max_iters,
    )


def _req(role: str = "Responsable", jwt: str | None = "jwt-x") -> AgentRunRequest:
    return AgentRunRequest(
        trace_id="t1",
        user_ctx=UserContext(user_id=1, role=role, jwt=jwt),
        messages=[ChatMessage(role="user", content="Bonjour")],
    )


async def _never_called(name, args, *, jwt):  # tool_caller for chat-only paths
    raise AssertionError("tool_caller should not be called")


@pytest.mark.asyncio
async def test_chat_only_run_yields_tokens_then_done():
    from agent import run_stream

    provider = MockLLMProvider(scripted_chunks=["Bon", "jour"], tokens_in=5, tokens_out=2)
    events = [
        ev async for ev in run_stream(
            _req(), provider=provider, settings=_settings(), tool_caller=_never_called
        )
    ]
    names = [n for n, _ in events]
    assert names == ["token", "token", "done"]
    assert events[-1][1]["tokens_in"] == 5


@pytest.mark.asyncio
async def test_tool_call_loop_executes_then_answers():
    from agent import run_stream

    provider = ScriptedLLMProvider(turns=[
        {"tool_call": {"id": "c1", "name": "get_student",
                       "arguments": '{"matricule": "20231042"}'}},
        {"text": ["Ahmed ", "est à risque modéré."]},
    ])

    calls = []

    async def tool_caller(name, args, *, jwt):
        calls.append((name, args, jwt))
        return {"ok": True, "data": {"matricule": "20231042", "risque": "modere"}}

    events = [
        ev async for ev in run_stream(
            _req(), provider=provider, settings=_settings(), tool_caller=tool_caller
        )
    ]
    names = [n for n, _ in events]

    assert "tool_call" in names
    assert "tool_result" in names
    assert names[-1] == "done"
    # Tool executed once with the validated args + forwarded JWT.
    assert calls == [("get_student", {"matricule": "20231042"}, "jwt-x")]
    # Final assistant text streamed after the tool result.
    text = "".join(p["text"] for n, p in events if n == "token")
    assert "risque modéré" in text


@pytest.mark.asyncio
async def test_invalid_tool_args_do_not_call_backend():
    from agent import run_stream

    provider = ScriptedLLMProvider(turns=[
        {"tool_call": {"id": "c1", "name": "get_student",
                       "arguments": '{"email": "a@b.ma"}'}},  # forbidden arg
        {"text": ["Désolé."]},
    ])

    async def tool_caller(name, args, *, jwt):
        raise AssertionError("backend must not be called on invalid args")

    events = [
        ev async for ev in run_stream(
            _req(), provider=provider, settings=_settings(), tool_caller=tool_caller
        )
    ]
    # A tool_result with ok=false (the L3 rejection) was emitted, loop continued.
    tool_results = [p for n, p in events if n == "tool_result"]
    assert tool_results and tool_results[0]["ok"] is False
    assert events[-1][0] == "done"


@pytest.mark.asyncio
async def test_loop_bound_emits_error_when_tools_never_stop():
    from agent import run_stream

    # Every turn asks for a tool again -> must hit the iteration cap (L6).
    turns = [
        {"tool_call": {"id": f"c{i}", "name": "get_student",
                       "arguments": '{"matricule": "42"}'}}
        for i in range(20)
    ]
    provider = ScriptedLLMProvider(turns=turns)

    async def tool_caller(name, args, *, jwt):
        return {"ok": True, "data": {}}

    events = [
        ev async for ev in run_stream(
            _req(), provider=provider, settings=_settings(max_iters=3),
            tool_caller=tool_caller
        )
    ]
    names = [n for n, _ in events]
    assert names[-1] == "error"
    # Exactly the capped number of tool executions.
    assert sum(1 for n in names if n == "tool_call") == 3
