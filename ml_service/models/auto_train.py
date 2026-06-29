"""
Auto-trains any missing model at startup.

Called by main.py lifespan before loading .joblib files.
If a model already exists on disk, training is skipped (fast path).
If it's missing (fresh clone, first docker compose up), it trains automatically.

Training priority:
   1. Real data from PFA_DW (via data.db_loader)
   2. UCI Student Performance dataset (via data.uci_loader)
   3. Synthetic data (via features.feature_engineering)

Uses shared feature engineering module — 5 features instead of original 3.
"""

import asyncio
import logging
import os
from pathlib import Path

from features import FEATURE_COLS

logger = logging.getLogger(__name__)

SAVED_MODELS_DIR = Path(os.getenv("MODEL_DIR", Path(__file__).parent.parent / "saved_models"))

_RANDOM_SEED = 42
_N_SAMPLES = 2000


def _ensure_risk_model() -> None:
    path = SAVED_MODELS_DIR / "risk_model.joblib"
    if path.exists():
        return

    from models.train_risk import train, save_with_metadata

    # ── 1. Try ENIAD DW first ────────────────────────────
    from data.db_loader import load_risk_data
    df = load_risk_data()
    data_source = "dw"
    if df is not None:
        logger.info("Training risk model on DW data (%d students, %d features)...",
                     len(df), len(FEATURE_COLS))

    # ── 2. Fall back to UCI baseline data ────────────────
    if df is None:
        from data.uci_loader import load_uci_risk_data
        df = load_uci_risk_data()
        data_source = "uci"
        if df is not None:
            logger.info("Training risk model on UCI data (%d students, %d features)...",
                         len(df), len(FEATURE_COLS))

    # ── 3. Fall back to synthetic ────────────────────────
    if df is None:
        from features import generate_synthetic_risk_data
        df = generate_synthetic_risk_data(_N_SAMPLES, _RANDOM_SEED)
        data_source = "synthetic"
        logger.info("Training risk model on SYNTHETIC data (%d samples, %d features)...",
                     _N_SAMPLES, len(FEATURE_COLS))

    pipeline, X_test, y_test, threshold_info = train(df)
    save_with_metadata(pipeline, X_test, y_test, threshold_info=threshold_info, data_source=data_source)
    logger.info("risk_model trained and saved (threshold=%s).",
                getattr(pipeline, "chosen_threshold", "0.5"))


def _ensure_cluster_model() -> None:
    path = SAVED_MODELS_DIR / "cluster_model.joblib"
    if path.exists():
        return

    from models.train_clustering import train, save_with_metadata

    # ── 1. Try ENIAD DW first ────────────────────────────
    from data.db_loader import load_clustering_data
    df = load_clustering_data()
    if df is not None:
        logger.info("Training cluster model on DW data (%d students, %d features)...",
                     len(df), len(FEATURE_COLS))

    # ── 2. Fall back to UCI baseline data ────────────────
    if df is None:
        from data.uci_loader import load_uci_clustering_data
        df = load_uci_clustering_data()
        if df is not None:
            logger.info("Training cluster model on UCI data (%d students, %d features)...",
                         len(df), len(FEATURE_COLS))

    # ── 3. Fall back to synthetic ────────────────────────
    if df is None:
        from features import generate_synthetic_cluster_data
        df = generate_synthetic_cluster_data(_N_SAMPLES, _RANDOM_SEED)
        logger.info("Training cluster model on SYNTHETIC data (%d samples, %d features)...",
                     _N_SAMPLES, len(FEATURE_COLS))

    pipeline = train(df, k=4)
    save_with_metadata(pipeline, k=4, n_samples=len(df))
    logger.info("cluster_model trained and saved.")


def _ensure_forecast_model() -> None:
    path = SAVED_MODELS_DIR / "forecast_model.joblib"
    if path.exists():
        return

    from models.train_regression import train, save_with_metadata

    # ── 1. Try ENIAD DW first ────────────────────────────
    from data.db_loader import load_forecast_data
    df = load_forecast_data()
    data_source = "dw"
    if df is not None:
        logger.info("Training forecast model on DW data (%d records, %d features)...",
                     len(df), len(FEATURE_COLS))

    # ── 2. Fall back to UCI baseline data ────────────────
    if df is None:
        from data.uci_loader import load_uci_forecast_data
        df = load_uci_forecast_data()
        data_source = "uci"
        if df is not None:
            logger.info("Training forecast model on UCI data (%d records, %d features)...",
                         len(df), len(FEATURE_COLS))

    # ── 3. Fall back to synthetic ────────────────────────
    if df is None:
        from features import generate_synthetic_forecast_data
        df = generate_synthetic_forecast_data(_N_SAMPLES, _RANDOM_SEED)
        data_source = "synthetic"
        logger.info("Training forecast model on SYNTHETIC data (%d samples, %d features)...",
                     _N_SAMPLES, len(FEATURE_COLS))

    pipeline, X_test, y_test = train(df)
    save_with_metadata(pipeline, X_test, y_test, data_source=data_source)
    logger.info("forecast_model trained and saved.")


async def ensure_all_models_async() -> None:
    SAVED_MODELS_DIR.mkdir(parents=True, exist_ok=True)
    await asyncio.to_thread(_ensure_risk_model)
    await asyncio.to_thread(_ensure_cluster_model)
    await asyncio.to_thread(_ensure_forecast_model)


def ensure_all_models() -> None:
    SAVED_MODELS_DIR.mkdir(parents=True, exist_ok=True)
    _ensure_risk_model()
    _ensure_cluster_model()
    _ensure_forecast_model()
