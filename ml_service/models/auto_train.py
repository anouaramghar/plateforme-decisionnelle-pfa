"""
Auto-trains any missing model at startup.

Called by main.py lifespan before loading .joblib files.
If a model already exists on disk, training is skipped (fast path).
If it's missing (fresh clone, first docker compose up), it trains automatically.

Training on 1000 synthetic students takes ~2-5 seconds per model — acceptable
for a startup delay. In production you would replace generate_data() calls
with real data loading functions.
"""

import logging
from pathlib import Path

logger = logging.getLogger(__name__)

SAVED_MODELS_DIR = Path(__file__).parent.parent / "saved_models"

_RANDOM_SEED = 42
_N_SAMPLES = 1000


def _ensure_risk_model() -> None:
    path = SAVED_MODELS_DIR / "risk_model.joblib"
    if path.exists():
        return
    logger.info("risk_model.joblib missing — training now (this runs once)...")
    from models.train_risk import generate_data, train, save
    df = generate_data(_N_SAMPLES, _RANDOM_SEED)
    pipeline = train(df)
    save(pipeline)
    logger.info("risk_model auto-trained and saved.")


def _ensure_cluster_model() -> None:
    path = SAVED_MODELS_DIR / "cluster_model.joblib"
    if path.exists():
        return
    logger.info("cluster_model.joblib missing — training now (this runs once)...")
    from models.train_clustering import generate_data, train, save
    df = generate_data(_N_SAMPLES, _RANDOM_SEED)
    pipeline = train(df, k=4)
    save(pipeline)
    logger.info("cluster_model auto-trained and saved.")


def _ensure_forecast_model() -> None:
    path = SAVED_MODELS_DIR / "forecast_model.joblib"
    if path.exists():
        return
    logger.info("forecast_model.joblib missing — training now (this runs once)...")
    from models.train_regression import generate_data, train, save
    df = generate_data(_N_SAMPLES, _RANDOM_SEED)
    pipeline = train(df)
    save(pipeline)
    logger.info("forecast_model auto-trained and saved.")


def ensure_all_models() -> None:
    """
    Entry point called by main.py lifespan.
    Creates saved_models/ if needed, then trains any missing model.
    Models already on disk are untouched.
    """
    SAVED_MODELS_DIR.mkdir(parents=True, exist_ok=True)
    _ensure_risk_model()
    _ensure_cluster_model()
    _ensure_forecast_model()
