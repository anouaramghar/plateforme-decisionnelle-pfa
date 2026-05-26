"""
ENIAD Copilot — agent-service.

FastAPI microservice that orchestrates the agent loop against NVIDIA NIM.
Reached only from the backend over the pfa_internal_ml Docker network;
unreachable from the public internet by design.

Pattern mirrors ml_service/main.py (lifespan, app.state, /health returns
'degraded' when the LLM provider is unreachable so depends_on stays accurate).
"""
from __future__ import annotations

import logging
from contextlib import asynccontextmanager

from fastapi import FastAPI
from fastapi.responses import JSONResponse

from config import get_settings

logger = logging.getLogger(__name__)


@asynccontextmanager
async def lifespan(app: FastAPI):
    settings = get_settings()  # fail-fast: missing env vars raise here
    app.state.settings = settings
    # Provider, agent loop, etc. attached in later tasks.
    logger.info(
        "agent-service starting. router=%s sql=%s reason=%s",
        settings.model_router, settings.model_sql, settings.model_reason,
    )
    yield
    logger.info("agent-service shutting down.")


app = FastAPI(
    title="PFA Copilot Agent Service",
    description="Agentic AI layer for Plateforme Décisionnelle ENIAD",
    version="0.1.0",
    lifespan=lifespan,
)


@app.get("/health", tags=["Health"])
def health():
    """
    Docker healthcheck + backend depends_on gate.

    'ok' when settings loaded. Future tasks may add provider readiness probes
    (L1 safety guard reachable, NIM reachable, etc.) and downgrade to
    'degraded' on partial failure.
    """
    settings_ok = getattr(app.state, "settings", None) is not None
    body = {
        "status": "ok" if settings_ok else "degraded",
        "service": "agent-service",
    }
    return JSONResponse(content=body, status_code=200 if settings_ok else 503)
