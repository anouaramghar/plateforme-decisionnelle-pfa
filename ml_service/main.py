from contextlib import asynccontextmanager
import asyncio
import logging
import os

logging.basicConfig(level=os.getenv("LOG_LEVEL", "INFO"), format="%(asctime)s %(levelname)s %(name)s: %(message)s")

from fastapi import FastAPI
from fastapi.responses import JSONResponse
import joblib

from models.auto_train import ensure_all_models_async, SAVED_MODELS_DIR
from routers import predict, cluster, forecast, metrics

logger = logging.getLogger(__name__)


# ─── Lifespan: startup & shutdown logic ───────────────────────────────────────

@asynccontextmanager
async def lifespan(app: FastAPI):
    """
    Runs once at startup (before yield) and once at shutdown (after yield).

    We load every trained model here and store them in app.state so any
    router can access them via request.app.state without reloading from disk.
    """
    # Fail-fast on missing or weak shared secret — never boot unauthenticated.
    _token = os.getenv("ML_INTERNAL_TOKEN", "")
    _PLACEHOLDER_TOKENS = {"change-me", "changeme", "placeholder", "your-secret-here", "xxx", "secret"}
    if len(_token) < 32 or _token in _PLACEHOLDER_TOKENS:
        raise RuntimeError(
            "ML_INTERNAL_TOKEN must be set to a random string of >= 32 characters "
            "and must not be a placeholder. Set it to the same value as the backend's ML_INTERNAL_TOKEN."
        )

    # Train any missing models before we try to load them.
    # On a warm container with saved_models/ already present this is instant.
    # Runs training off the event loop via asyncio.to_thread so liveness probes
    # stay responsive during startup.
    await ensure_all_models_async()

    logger.info("Loading ML models...")

    # Load the risk classifier — required for /predict to work.
    risk_model_path = SAVED_MODELS_DIR / "risk_model.joblib"
    if risk_model_path.exists():
        app.state.risk_model = joblib.load(risk_model_path)
        logger.info("risk_model loaded OK")
    else:
        app.state.risk_model = None
        logger.warning("risk_model.joblib not found — /predict will return 503")

    # Load the K-Means clustering model — required for /cluster to work.
    cluster_model_path = SAVED_MODELS_DIR / "cluster_model.joblib"
    if cluster_model_path.exists():
        app.state.cluster_model = joblib.load(cluster_model_path)
        logger.info("cluster_model loaded OK")
    else:
        app.state.cluster_model = None
        logger.warning("cluster_model.joblib not found — /cluster will return 503")

    # Load the grade forecast regressor — required for /forecast to work.
    forecast_model_path = SAVED_MODELS_DIR / "forecast_model.joblib"
    if forecast_model_path.exists():
        app.state.forecast_model = joblib.load(forecast_model_path)
        logger.info("forecast_model loaded OK")
    else:
        app.state.forecast_model = None
        logger.warning("forecast_model.joblib not found — /forecast will return 503")

    yield  # <-- FastAPI serves requests from here until shutdown

    # Shutdown: nothing to clean up for joblib models.
    logger.info("ML service shutting down.")


# ─── App definition ───────────────────────────────────────────────────────────

_enable_docs = os.getenv("ML_ENABLE_DOCS", "1") == "1"
app = FastAPI(
    title="PFA ML Service",
    description="Microservice ML — Plateforme Décisionnelle ENIAD",
    version="1.0.0",
    lifespan=lifespan,
    docs_url="/docs" if _enable_docs else None,
    redoc_url="/redoc" if _enable_docs else None,
)


# ─── Routers ──────────────────────────────────────────────────────────────────

app.include_router(predict.router)
app.include_router(cluster.router)
app.include_router(forecast.router)
app.include_router(metrics.router)


# ─── Health probe ─────────────────────────────────────────────────────────────

@app.get("/health", tags=["Health"])
def health():
    """
    Used by Docker healthcheck and the backend's depends_on gate.
    Returns which models are loaded so you can diagnose startup issues.

    Returns 503 when the risk_model (the primary classifier driving /predict)
    failed to load — otherwise Docker would mark the container healthy and the
    backend would start serving 503s on every prediction call. cluster_model
    and forecast_model are optional and do not block readiness.
    """
    risk_loaded = getattr(app.state, "risk_model", None) is not None
    body = {
        "status": "ok" if risk_loaded else "degraded",
        "service": "ml-service",
        "models": {
            "risk_model": risk_loaded,
            "cluster_model": getattr(app.state, "cluster_model", None) is not None,
            "forecast_model": getattr(app.state, "forecast_model", None) is not None,
        },
    }
    return JSONResponse(content=body, status_code=200 if risk_loaded else 503)
