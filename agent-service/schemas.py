"""
Pydantic models shared between agent-service routes and the agent loop.

- Request models accepted by /agent/run.
- Event models serialized into SSE `data:` payloads streamed back to backend.

Event names (used as the SSE `event:` field) are exactly:
    token | tool_call | tool_result | confirm_request | safety_block | done | error

Frontend dispatches on these — see spec §4.2.
"""
from __future__ import annotations

from typing import Any, Literal

from pydantic import BaseModel, Field


# ─── Request side ────────────────────────────────────────────────────────────

ChatRole = Literal["system", "user", "assistant", "tool"]


class ChatMessage(BaseModel):
    role: ChatRole
    content: str | None = None
    # tool_calls populated on assistant turns that called tools; tool_call_id
    # + name populated on tool turns. Kept loose because OpenAI's exact shape
    # evolves; we just round-trip whatever the SDK gives us.
    tool_calls: list[dict[str, Any]] | None = None
    tool_call_id: str | None = None
    name: str | None = None


class UserContext(BaseModel):
    user_id: int
    role: str
    # Forwarded JWT so tool calls (P1.B) can re-enter backend AS the user.
    jwt: str | None = None
    # Current page on the frontend — auto-injected into the system prompt.
    page_context: dict[str, Any] | None = None


class AgentRunRequest(BaseModel):
    trace_id: str
    user_ctx: UserContext
    messages: list[ChatMessage] = Field(default_factory=list)


# ─── Event side (one model per SSE `event:` name) ────────────────────────────


class TokenEvent(BaseModel):
    text: str


class ToolCallEvent(BaseModel):
    name: str
    args: dict[str, Any]
    call_id: str


class ToolResultEvent(BaseModel):
    name: str
    call_id: str
    ok: bool
    summary: str | None = None
    error: str | None = None


class ConfirmRequestEvent(BaseModel):
    draft_id: int
    preview: dict[str, Any]
    tool: str


class SafetyBlockEvent(BaseModel):
    reason: str


class DoneEvent(BaseModel):
    tokens_in: int
    tokens_out: int
    latency_ms: int


class ErrorEvent(BaseModel):
    message: str
