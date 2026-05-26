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
    internal_token: str = Field(..., alias="ML_INTERNAL_TOKEN")

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

    model_config = {
        "case_sensitive": False,
        "populate_by_name": True,
    }


@lru_cache(maxsize=1)
def get_settings() -> Settings:
    return Settings()  # type: ignore[call-arg]
