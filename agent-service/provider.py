"""
LLMProvider interface — the only place agent-service knows about a specific
LLM vendor. Swapping NIM for Anthropic or local Ollama is a one-class change
(see spec §6.4).

Two implementations:
- MockLLMProvider: scripted chunks, no network. Used in unit tests.
- NIMProvider: real NVIDIA NIM via the OpenAI-compatible API.
"""
from __future__ import annotations

import asyncio
from abc import ABC, abstractmethod
from collections.abc import AsyncIterator
from dataclasses import dataclass
from typing import Any

from openai import AsyncOpenAI

from schemas import ChatMessage


@dataclass
class StreamDelta:
    """One incremental piece of model output.

    Exactly one of `text` or `tool_call` is set per delta. `finish_reason`
    is set on the final delta only ('stop' | 'tool_calls' | 'length' | None).
    """
    text: str | None = None
    tool_call: dict[str, Any] | None = None
    finish_reason: str | None = None


class LLMProvider(ABC):
    """Abstract base. All providers must support streaming chat completions."""

    @abstractmethod
    def chat_stream(
        self,
        *,
        model: str,
        messages: list[ChatMessage],
        tools: list[dict[str, Any]] | None = None,
        temperature: float = 0.0,
        max_tokens: int | None = None,
    ) -> AsyncIterator[StreamDelta]:
        """Yield StreamDelta objects until the model emits a finish reason."""
        ...


class MockLLMProvider(LLMProvider):
    """Test-only provider that replays a scripted sequence of text chunks."""

    def __init__(
        self,
        scripted_chunks: list[str],
        tokens_in: int = 0,
        tokens_out: int = 0,
        finish_reason: str = "stop",
    ):
        self.scripted_chunks = scripted_chunks
        self.tokens_in = tokens_in
        self.tokens_out = tokens_out
        self.finish_reason = finish_reason
        self.last_tokens_in: int = 0
        self.last_tokens_out: int = 0

    async def chat_stream(
        self,
        *,
        model: str,
        messages: list[ChatMessage],
        tools: list[dict[str, Any]] | None = None,
        temperature: float = 0.0,
        max_tokens: int | None = None,
    ) -> AsyncIterator[StreamDelta]:
        for chunk in self.scripted_chunks:
            yield StreamDelta(text=chunk)
            await asyncio.sleep(0)  # cooperative scheduling
        self.last_tokens_in = self.tokens_in
        self.last_tokens_out = self.tokens_out
        yield StreamDelta(finish_reason=self.finish_reason)


class ScriptedLLMProvider(LLMProvider):
    """
    Multi-turn test provider. Each chat_stream() call consumes the next scripted
    turn. A turn is either:
      {"text": ["chunk", ...]}                      -> finish_reason "stop"
      {"tool_call": {"id","name","arguments"}}      -> finish_reason "tool_calls"
    `arguments` is a JSON string, matching what NIMProvider emits.
    """

    def __init__(self, turns: list[dict[str, Any]]):
        self._turns = list(turns)
        self.last_tokens_in: int = 0
        self.last_tokens_out: int = 0

    async def chat_stream(
        self,
        *,
        model: str,
        messages: list[ChatMessage],
        tools: list[dict[str, Any]] | None = None,
        temperature: float = 0.0,
        max_tokens: int | None = None,
    ) -> AsyncIterator[StreamDelta]:
        if not self._turns:
            yield StreamDelta(text="(no more scripted turns)")
            yield StreamDelta(finish_reason="stop")
            return

        turn = self._turns.pop(0)
        if "tool_call" in turn:
            yield StreamDelta(tool_call=turn["tool_call"])
            yield StreamDelta(finish_reason="tool_calls")
        else:
            for chunk in turn.get("text", []):
                yield StreamDelta(text=chunk)
            yield StreamDelta(finish_reason="stop")


class NIMProvider(LLMProvider):
    """
    NVIDIA NIM (OpenAI-compatible). Free tier latency is highly variable
    (1s-100s+); the agent loop relies on streaming to mask this — see spec §6.3.
    """

    def __init__(self, api_key: str, base_url: str):
        self._client = AsyncOpenAI(api_key=api_key, base_url=base_url)
        # Usage from the most recent stream — read by the agent loop on the
        # final delta to populate DoneEvent.tokens_* (and the P1.B token cap).
        self.last_tokens_in: int = 0
        self.last_tokens_out: int = 0

    async def chat_stream(
        self,
        *,
        model: str,
        messages: list[ChatMessage],
        tools: list[dict[str, Any]] | None = None,
        temperature: float = 0.0,
        max_tokens: int | None = None,
    ) -> AsyncIterator[StreamDelta]:
        # Reset usage for this call.
        self.last_tokens_in = 0
        self.last_tokens_out = 0

        # Convert our ChatMessage shape -> OpenAI dicts.
        openai_messages: list[dict[str, Any]] = []
        for m in messages:
            entry: dict[str, Any] = {"role": m.role}
            if m.content is not None:
                entry["content"] = m.content
            if m.tool_calls:
                entry["tool_calls"] = m.tool_calls
            if m.tool_call_id:
                entry["tool_call_id"] = m.tool_call_id
            if m.name:
                entry["name"] = m.name
            openai_messages.append(entry)

        kwargs: dict[str, Any] = {
            "model": model,
            "messages": openai_messages,
            "temperature": temperature,
            "stream": True,
            # Ask NIM to emit a trailing usage-only chunk so we can report real
            # token counts on the done event (OpenAI-compatible streaming omits
            # usage otherwise).
            "stream_options": {"include_usage": True},
        }
        if tools:
            kwargs["tools"] = tools
            kwargs["tool_choice"] = "auto"
        if max_tokens is not None:
            kwargs["max_tokens"] = max_tokens

        stream = await self._client.chat.completions.create(**kwargs)

        # Tool-call deltas arrive in pieces (index, id, name, arguments-fragment).
        # We accumulate per-index and only emit a tool_call StreamDelta once the
        # finish_reason is "tool_calls" — at that point all fragments are present.
        tool_buffers: dict[int, dict[str, Any]] = {}
        finish_reason: str | None = None

        async for chunk in stream:
            # The usage-only chunk (and sometimes the final content chunk) carries
            # usage with empty choices. Capture it whenever present.
            usage = getattr(chunk, "usage", None)
            if usage is not None:
                self.last_tokens_in = usage.prompt_tokens or 0
                self.last_tokens_out = usage.completion_tokens or 0

            if not chunk.choices:
                continue
            choice = chunk.choices[0]
            delta = choice.delta

            if delta.content:
                yield StreamDelta(text=delta.content)

            if delta.tool_calls:
                for tc in delta.tool_calls:
                    idx = tc.index
                    buf = tool_buffers.setdefault(
                        idx, {"id": None, "name": None, "arguments": ""}
                    )
                    if tc.id:
                        buf["id"] = tc.id
                    if tc.function and tc.function.name:
                        buf["name"] = tc.function.name
                    if tc.function and tc.function.arguments:
                        buf["arguments"] += tc.function.arguments

            if choice.finish_reason:
                # Don't return here: with include_usage a trailing usage-only
                # chunk still follows. Remember the reason and keep draining the
                # stream so last_tokens_* are set before we emit the final delta.
                finish_reason = choice.finish_reason

        # Stream exhausted (usage chunk, if any, has been consumed).
        # Emit buffered tool calls when reason is "tool_calls" OR when the stream
        # drained without a reason chunk (network reset before the final chunk) but
        # we accumulated tool-call fragments — treat that as an implicit "tool_calls".
        if tool_buffers and finish_reason in ("tool_calls", None):
            for idx in sorted(tool_buffers):
                yield StreamDelta(tool_call=tool_buffers[idx])
            if finish_reason is None:
                finish_reason = "tool_calls"
        yield StreamDelta(finish_reason=finish_reason or "stop")
