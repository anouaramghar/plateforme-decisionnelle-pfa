from contextlib import asynccontextmanager
from pathlib import Path
import asyncio
import logging
import os

from fastapi import FastAPI
import joblib

from models.auto_train import ensure_all_models
from routers import predict, cluster, forecast, metrics

logger = logging.getLogger(__name__)

SAVED_MODELS_DIR = Path(__file__).parent / "saved_models"


# ─── Lifespan: startup & shutdown logic ───────────────────────────────────────

@asynccontextmanager
async def lifespan(app: FastAPI):
    """
    Runs once at startup (before yield) and once at shutdown (after yield).

    We load every trained model here and store them in app.state so any
    router can access them via request.app.state without reloading from disk.
    """
    # Fail-fast on missing shared secret — never boot in an unauthenticated state.
    if not os.getenv("ML_INTERNAL_TOKEN"):
        raise RuntimeError(
            "ML_INTERNAL_TOKEN environment variable is required. "
            "Set it to the same value as the backend's ML_INTERNAL_TOKEN."
        )

    # Train any missing models before we try to load them.
    # On a warm container with saved_models/ already present this is instant.
    # Run training off the event loop to keep liveness probes responsive.
    await asyncio.to_thread(ensure_all_models)

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

app = FastAPI(
    title="PFA ML Service",
    description="Microservice ML — Plateforme Décisionnelle ENIAD",
    version="1.0.0",
    lifespan=lifespan,
    # Hide /docs and /redoc in production (set via env var).
    # During development they are useful — keep them on.
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
    """
    return {
        "status": "ok",
        "service": "ml-service",
        "models": {
            "risk_model": app.state.risk_model is not None,
            "cluster_model": app.state.cluster_model is not None,
            "forecast_model": app.state.forecast_model is not None,
        },
    }
