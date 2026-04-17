from contextlib import asynccontextmanager
from pathlib import Path
import logging

from fastapi import FastAPI
import joblib

from routers import predict

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
    logger.info("Loading ML models...")

    # Load the risk classifier — required for /predict to work.
    risk_model_path = SAVED_MODELS_DIR / "risk_model.joblib"
    if risk_model_path.exists():
        app.state.risk_model = joblib.load(risk_model_path)
        logger.info("risk_model loaded OK")
    else:
        # Not a fatal error — the app still starts, but /predict returns 503
        # until the model is trained and placed in saved_models/.
        app.state.risk_model = None
        logger.warning("risk_model.joblib not found — /predict will return 503")

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

# Each router handles one ML feature. Add cluster and forecast here
# once their models are trained.
app.include_router(predict.router)


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
        },
    }
