"""
GET /metrics — returns AUC, F1, precision, recall for the risk model.

Two paths:
  1. "computed"  — eval_set.parquet exists: recomputes metrics live with sklearn.
  2. "metadata"  — no parquet, falls back to last-known values from metadata.json.

Never 500s: if neither file exists, returns zeros with source="metadata".
"""
from __future__ import annotations

import json
import logging
from pathlib import Path
from typing import Literal

import pandas as pd
from fastapi import APIRouter, Depends
from pydantic import BaseModel
from sklearn.metrics import (
    f1_score,
    precision_score,
    recall_score,
    roc_auc_score,
)

from dependencies import verify_internal_token

logger = logging.getLogger(__name__)

router = APIRouter(prefix="/metrics", tags=["Metrics"])

# Both paths below are relative to this file's location.
# Inside the Docker container WORKDIR=/app so __file__ is /app/routers/metrics.py
# and saved_models/ is /app/saved_models/ — same result.
_SAVED_DIR = Path(__file__).parent.parent / "saved_models"
_EVAL_PARQUET = _SAVED_DIR / "eval_set.parquet"
_METADATA_JSON = _SAVED_DIR / "metadata.json"


# ─── Response schema ──────────────────────────────────────────────────────────

class MetricsResponse(BaseModel):
    model_config = {"protected_namespaces": ()}

    auc: float
    f1: float
    precision: float
    recall: float
    n_samples: int
    model_version: str
    trained_at: str | None
    source: Literal["computed", "metadata"]


# ─── Helpers ──────────────────────────────────────────────────────────────────

def _load_metadata() -> dict:
    """Returns metadata dict, or safe defaults if the file doesn't exist."""
    if _METADATA_JSON.exists():
        try:
            return json.loads(_METADATA_JSON.read_text(encoding="utf-8"))
        except Exception as exc:
            logger.warning("Could not parse metadata.json: %s", exc)
    return {}


def _compute_from_eval_set(pipeline) -> MetricsResponse:
    """Recomputes metrics from eval_set.parquet using the loaded model."""
    df = pd.read_parquet(_EVAL_PARQUET)
    X = df[["moyenne_generale", "taux_absence", "nb_modules"]]
    y_true = df["y_true"]

    y_pred = pipeline.predict(X)
    y_proba = pipeline.predict_proba(X)[:, 1]

    meta = _load_metadata()

    return MetricsResponse(
        auc=round(float(roc_auc_score(y_true, y_proba)), 4),
        f1=round(float(f1_score(y_true, y_pred, zero_division=0)), 4),
        precision=round(float(precision_score(y_true, y_pred, zero_division=0)), 4),
        recall=round(float(recall_score(y_true, y_pred, zero_division=0)), 4),
        n_samples=len(df),
        model_version=meta.get("model_version", "unknown"),
        trained_at=meta.get("trained_at"),
        source="computed",
    )


def _from_metadata() -> MetricsResponse:
    """Returns last-known metrics from metadata.json, or safe zeros."""
    meta = _load_metadata()
    return MetricsResponse(
        auc=float(meta.get("auc", 0.0)),
        f1=float(meta.get("f1", 0.0)),
        precision=float(meta.get("precision", 0.0)),
        recall=float(meta.get("recall", 0.0)),
        n_samples=int(meta.get("n_samples", 0)),
        model_version=meta.get("model_version", "unknown"),
        trained_at=meta.get("trained_at"),
        source="metadata",
    )


# ─── Endpoint ─────────────────────────────────────────────────────────────────

@router.get("", response_model=MetricsResponse)
def get_metrics(
    _: None = Depends(verify_internal_token),
) -> MetricsResponse:
    """
    Returns evaluation metrics for the risk classifier.

    - If eval_set.parquet is present, metrics are recomputed live on that
      held-out set (source = "computed").
    - Otherwise the last-persisted values from metadata.json are returned
      (source = "metadata").
    - If neither file exists, safe zeros are returned (source = "metadata").
      The endpoint never raises a 500.
    """
    if _EVAL_PARQUET.exists():
        # Try to load the model from app state via a Request dependency.
        # We do a lazy import of joblib so this module stays importable without
        # a running app (useful for pytest).
        try:
            import joblib
            risk_model_path = _SAVED_DIR / "risk_model.joblib"
            if risk_model_path.exists():
                pipeline = joblib.load(risk_model_path)
                return _compute_from_eval_set(pipeline)
        except Exception as exc:
            logger.warning(
                "Failed to compute metrics from eval set (%s). "
                "Falling back to metadata.json.",
                exc,
            )

    return _from_metadata()
