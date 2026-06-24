"""
GET /metrics — returns metrics for both risk and forecast models.
"""
from __future__ import annotations

import json
import logging
from pathlib import Path
from typing import Literal

import pandas as pd
from fastapi import APIRouter, Depends, Request
from pydantic import BaseModel
from sklearn.metrics import (
    f1_score,
    precision_score,
    recall_score,
    roc_auc_score,
)

from dependencies import verify_internal_token
from models.auto_train import SAVED_MODELS_DIR

logger = logging.getLogger(__name__)

router = APIRouter(prefix="/metrics", tags=["Metrics"])

_SAVED_DIR = SAVED_MODELS_DIR
_EVAL_PARQUET = _SAVED_DIR / "eval_set.parquet"
_METADATA_JSON = _SAVED_DIR / "metadata.json"

_FORECAST_EVAL_PARQUET = _SAVED_DIR / "forecast_eval_set.parquet"
_FORECAST_METADATA_JSON = _SAVED_DIR / "forecast_metadata.json"


# ─── Response schema ──────────────────────────────────────────────────────────

class ModelMetrics(BaseModel):
    auc: float | None = None
    f1: float | None = None
    precision: float | None = None
    recall: float | None = None
    mae: float | None = None
    r2: float | None = None
    n_samples: int
    model_version: str
    trained_at: str | None
    data_source: str
    split_strategy: str
    source: str
    train_periods: list[float] = []
    test_periods: list[float] = []
    n_students_train: int = 0
    n_students_test: int = 0


class MetricsResponse(BaseModel):
    model_config = {"protected_namespaces": ()}

    risk: ModelMetrics
    forecast: ModelMetrics

    # Root properties for backward compatibility
    auc: float | None = None
    f1: float | None = None
    precision: float | None = None
    recall: float | None = None
    n_samples: int | None = None
    model_version: str | None = None
    trained_at: str | None = None
    source: str | None = None


# ─── Helpers ──────────────────────────────────────────────────────────────────

def _load_metadata(file_path: Path) -> dict:
    """Returns metadata dict, or safe defaults if the file doesn't exist."""
    if file_path.exists():
        try:
            return json.loads(file_path.read_text(encoding="utf-8"))
        except Exception as exc:
            logger.warning("Could not parse %s: %s", file_path, exc)
    return {}


def _compute_risk(pipeline) -> ModelMetrics:
    """Computes or loads risk model metrics."""
    if _EVAL_PARQUET.exists() and pipeline is not None:
        try:
            df = pd.read_parquet(_EVAL_PARQUET)
            X = df[["moyenne_generale", "taux_absence", "nb_modules"]]
            y_true = df["y_true"]

            y_pred = pipeline.predict(X)
            y_proba = pipeline.predict_proba(X)[:, 1]

            meta = _load_metadata(_METADATA_JSON)

            try:
                auc_val = float(roc_auc_score(y_true, y_proba))
            except ValueError:
                auc_val = 0.0

            return ModelMetrics(
                auc=round(auc_val, 4),
                f1=round(float(f1_score(y_true, y_pred, zero_division=0)), 4),
                precision=round(float(precision_score(y_true, y_pred, zero_division=0)), 4),
                recall=round(float(recall_score(y_true, y_pred, zero_division=0)), 4),
                n_samples=len(df),
                model_version=meta.get("model_version", "unknown"),
                trained_at=meta.get("trained_at"),
                data_source=meta.get("data_source", "unknown"),
                split_strategy=meta.get("split_strategy", "unknown"),
                source="computed",
                train_periods=meta.get("train_periods", []),
                test_periods=meta.get("test_periods", []),
                n_students_train=meta.get("n_students_train", 0),
                n_students_test=meta.get("n_students_test", 0)
            )
        except Exception as exc:
            logger.warning("Failed to compute risk metrics from eval set: %s", exc)

    meta = _load_metadata(_METADATA_JSON)
    return ModelMetrics(
        auc=float(meta.get("auc", 0.0)),
        f1=float(meta.get("f1", 0.0)),
        precision=float(meta.get("precision", 0.0)),
        recall=float(meta.get("recall", 0.0)),
        n_samples=int(meta.get("n_samples", 0)),
        model_version=meta.get("model_version", "unknown"),
        trained_at=meta.get("trained_at"),
        data_source=meta.get("data_source", "unknown"),
        split_strategy=meta.get("split_strategy", "unknown"),
        source="metadata",
        train_periods=meta.get("train_periods", []),
        test_periods=meta.get("test_periods", []),
        n_students_train=meta.get("n_students_train", 0),
        n_students_test=meta.get("n_students_test", 0)
    )


def _compute_forecast(pipeline) -> ModelMetrics:
    """Computes or loads forecast model metrics."""
    from sklearn.metrics import mean_absolute_error, r2_score
    import numpy as np

    if _FORECAST_EVAL_PARQUET.exists() and pipeline is not None:
        try:
            df = pd.read_parquet(_FORECAST_EVAL_PARQUET)
            X = df[["moyenne_actuelle", "taux_absence", "nb_modules"]]
            y_true = df["y_true"]

            y_pred = pipeline.predict(X)
            y_pred_clipped = np.clip(y_pred, 0, 20)

            meta = _load_metadata(_FORECAST_METADATA_JSON)

            return ModelMetrics(
                mae=round(float(mean_absolute_error(y_true, y_pred_clipped)), 4),
                r2=round(float(r2_score(y_true, y_pred_clipped)), 4),
                n_samples=len(df),
                model_version=meta.get("model_version", "unknown"),
                trained_at=meta.get("trained_at"),
                data_source=meta.get("data_source", "unknown"),
                split_strategy=meta.get("split_strategy", "unknown"),
                source="computed",
                train_periods=meta.get("train_periods", []),
                test_periods=meta.get("test_periods", []),
                n_students_train=meta.get("n_students_train", 0),
                n_students_test=meta.get("n_students_test", 0)
            )
        except Exception as exc:
            logger.warning("Failed to compute forecast metrics from eval set: %s", exc)

    meta = _load_metadata(_FORECAST_METADATA_JSON)
    return ModelMetrics(
        mae=float(meta.get("mae", 0.0)),
        r2=float(meta.get("r2", 0.0)),
        n_samples=int(meta.get("n_samples", 0)),
        model_version=meta.get("model_version", "unknown"),
        trained_at=meta.get("trained_at"),
        data_source=meta.get("data_source", "unknown"),
        split_strategy=meta.get("split_strategy", "unknown"),
        source="metadata",
        train_periods=meta.get("train_periods", []),
        test_periods=meta.get("test_periods", []),
        n_students_train=meta.get("n_students_train", 0),
        n_students_test=meta.get("n_students_test", 0)
    )


# ─── Endpoint ─────────────────────────────────────────────────────────────────

@router.get("", response_model=MetricsResponse)
def get_metrics(
    request: Request,
    _: None = Depends(verify_internal_token),
) -> MetricsResponse:
    """
    Returns evaluation metrics for both risk classifier and grade forecaster.
    """
    risk_pipeline = getattr(request.app.state, "risk_model", None)
    risk_metrics = _compute_risk(risk_pipeline)

    forecast_pipeline = getattr(request.app.state, "forecast_model", None)
    forecast_metrics = _compute_forecast(forecast_pipeline)

    return MetricsResponse(
        risk=risk_metrics,
        forecast=forecast_metrics,
        # Root level properties mapped to risk for backward compatibility
        auc=risk_metrics.auc,
        f1=risk_metrics.f1,
        precision=risk_metrics.precision,
        recall=risk_metrics.recall,
        n_samples=risk_metrics.n_samples,
        model_version=risk_metrics.model_version,
        trained_at=risk_metrics.trained_at,
        source=risk_metrics.source
    )
