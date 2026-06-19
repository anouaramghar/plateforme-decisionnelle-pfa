"""
Environment-backed settings for agent-service. Reads from process env vars
(populated by docker-compose). pydantic-settings raises at startup if anything
required is missing — fail-fast, same pattern as ml_service.
"""
from __future__ import annotations

from functools import lru_cache

from pydantic import Field
from pydantic_settings import BaseSettings


class Settings(BaseSettings):
    # Shared secret with backend (X-Internal-Token on /agent/*).
    # Distinct from ML_INTERNAL_TOKEN so leak of one does not compromise both.
    internal_token: str = Field(..., alias="AGENT_INTERNAL_TOKEN")

    # NIM provider
    nim_api_key: str = Field(..., alias="NVIDIA_NIM_API_KEY")
    nim_base_url: str = Field(
        default="https://integrate.api.nvidia.com/v1",
        alias="NVIDIA_NIM_BASE_URL",
    )

    # Models (overridable per-deployment).
    model_router: str = Field(
        default="meta/llama-3.3-70b-instruct",
        alias="COPILOT_MODEL_ROUTER",
    )
    model_sql: str = Field(
        default="minimaxai/minimax-m2.7",
        alias="COPILOT_MODEL_SQL",
    )
    model_reason: str = Field(
        default="nvidia/llama-3.3-nemotron-super-49b-v1.5",
        alias="COPILOT_MODEL_REASON",
    )
    model_safety: str = Field(
        default="nvidia/llama-3.1-nemotron-safety-guard-8b-v3",
        alias="COPILOT_MODEL_SAFETY",
    )
    embed_model: str = Field(
        default="nvidia/nv-embedqa-e5-v5",
        alias="COPILOT_EMBED_MODEL",
    )

    # Limits (L6 of the safety layer).
    max_tool_iterations: int = Field(
        default=8, alias="COPILOT_MAX_TOOL_ITERATIONS"
    )
    turn_budget_in: int = Field(
        default=20_000, alias="COPILOT_TURN_TOKEN_BUDGET_IN"
    )
    turn_budget_out: int = Field(
        default=4_000, alias="COPILOT_TURN_TOKEN_BUDGET_OUT"
    )
    # Per-user daily token ceiling. Enforcement lands in P1.B once token usage
    # is accounted (DoneEvent.tokens_*). Loaded here so the env contract is
    # stable and validated at startup.
    daily_token_cap_per_user: int = Field(
        default=200_000, alias="COPILOT_DAILY_TOKEN_CAP_PER_USER"
    )
    # Read-only DW connection for the P1.B query_dw tool. Optional until then.
    dw_readonly_conn: str | None = Field(
        default=None, alias="COPILOT_DW_READONLY_CONN"
    )
    # Where agent-service reaches the backend to execute tools (L2 callback).
    # Resolves over pfa_internal_ml; never the public nginx host.
    backend_internal_url: str = Field(
        default="http://backend:8080", alias="BACKEND_INTERNAL_URL"
    )

    model_config = {
        "case_sensitive": False,
        "populate_by_name": True,
    }


@lru_cache(maxsize=1)
def get_settings() -> Settings:
    return Settings()  # type: ignore[call-arg]
