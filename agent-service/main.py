"""
ENIAD Copilot — agent-service.

FastAPI microservice that orchestrates the agent loop against NVIDIA NIM.
Reached only from the backend over the pfa_internal_ml Docker network;
unreachable from the public internet by design.

Pattern mirrors ml_service/main.py (lifespan, app.state, /health returns
'degraded' when the LLM provider is unreachable so depends_on stays accurate).
"""
from __future__ import annotations

import json
import logging
from contextlib import asynccontextmanager

from fastapi import Depends, FastAPI, Request
from fastapi.responses import JSONResponse, StreamingResponse

from agent import run_stream
from auth import require_internal_token
from config import get_settings
from provider import NIMProvider
from schemas import AgentRunRequest

logger = logging.getLogger(__name__)


@asynccontextmanager
async def lifespan(app: FastAPI):
    settings = get_settings()  # fail-fast: missing env vars raise here
    app.state.settings = settings
    app.state.provider = NIMProvider(
        api_key=settings.nim_api_key, base_url=settings.nim_base_url,
    )
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


def _sse_format(event: str, data: dict) -> bytes:
    return f"event: {event}\ndata: {json.dumps(data, ensure_ascii=False)}\n\n".encode("utf-8")


@app.post("/agent/run", dependencies=[Depends(require_internal_token)])
async def agent_run(req: AgentRunRequest, request: Request):
    """
    Stream the agent loop as SSE. Backend proxies these events to the browser
    unchanged. See spec §4.2 for the event vocabulary.
    """
    provider = request.app.state.provider
    settings = request.app.state.settings

    async def gen():
        async for name, payload in run_stream(
            req, provider=provider, model=settings.model_router,
        ):
            yield _sse_format(name, payload)

    return StreamingResponse(
        gen(),
        media_type="text/event-stream",
        headers={
            "Cache-Control": "no-cache",
            "X-Accel-Buffering": "no",   # nginx: disable response buffering
        },
    )
