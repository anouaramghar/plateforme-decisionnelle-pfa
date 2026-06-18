"""
Auto-trains any missing model at startup.

Called by main.py lifespan before loading .joblib files.
If a model already exists on disk, training is skipped (fast path).
If it's missing (fresh clone, first docker compose up), it trains automatically.

Training priority:
  1. Real data from PFA_DW (via data.db_loader)  ← preferred
  2. Synthetic data (via generate_data())         ← fallback

Training on 1000 synthetic students takes ~2-5 seconds per model — acceptable
for a startup delay.
"""

import asyncio
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

    from data.db_loader import load_risk_data
    from models.train_risk import generate_data, train, save_with_metadata

    df = load_risk_data()
    if df is not None:
        logger.info("Training risk model on REAL DW data (%d students)...", len(df))
        data_source = "dw"
    else:
        logger.info("Training risk model on SYNTHETIC data (%d samples)...", _N_SAMPLES)
        df = generate_data(_N_SAMPLES, _RANDOM_SEED)
        data_source = "synthetic"

    pipeline, X_test, y_test = train(df)
    save_with_metadata(pipeline, X_test, y_test, data_source=data_source)
    logger.info("risk_model trained and saved.")


def _ensure_cluster_model() -> None:
    path = SAVED_MODELS_DIR / "cluster_model.joblib"
    if path.exists():
        return

    from data.db_loader import load_clustering_data
    from models.train_clustering import generate_data, train, save

    df = load_clustering_data()
    if df is not None:
        logger.info("Training cluster model on REAL DW data (%d students)...", len(df))
    else:
        logger.info("Training cluster model on SYNTHETIC data (%d samples)...", _N_SAMPLES)
        df = generate_data(_N_SAMPLES, _RANDOM_SEED)

    pipeline = train(df, k=4)
    save(pipeline)
    logger.info("cluster_model trained and saved.")


def _ensure_forecast_model() -> None:
    path = SAVED_MODELS_DIR / "forecast_model.joblib"
    if path.exists():
        return

    from data.db_loader import load_forecast_data
    from models.train_regression import generate_data, train, save

    df = load_forecast_data()
    if df is not None:
        logger.info("Training forecast model on REAL DW data (%d records)...", len(df))
    else:
        logger.info("Training forecast model on SYNTHETIC data (%d samples)...", _N_SAMPLES)
        df = generate_data(_N_SAMPLES, _RANDOM_SEED)

    pipeline = train(df)
    save(pipeline)
    logger.info("forecast_model trained and saved.")


async def ensure_all_models_async() -> None:
    """
    Async entry point — runs blocking training off the event loop so
    Docker health probes stay responsive during startup.
    """
    SAVED_MODELS_DIR.mkdir(parents=True, exist_ok=True)
    await asyncio.to_thread(_ensure_risk_model)
    await asyncio.to_thread(_ensure_cluster_model)
    await asyncio.to_thread(_ensure_forecast_model)


def ensure_all_models() -> None:
    """
    Synchronous entry point — kept for backward compatibility with
    retrain endpoint and direct script invocation.
    """
    SAVED_MODELS_DIR.mkdir(parents=True, exist_ok=True)
    _ensure_risk_model()
    _ensure_cluster_model()
    _ensure_forecast_model()
