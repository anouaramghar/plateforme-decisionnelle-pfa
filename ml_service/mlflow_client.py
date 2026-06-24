"""
Best-effort MLflow tracking integration for the ML service.

Every training run logs params, metrics, tags, and model artifacts to an
external MLflow tracking server when MLFLOW_TRACKING_URI is set and reachable.
If MLflow is not configured or down, training proceeds normally — logging is
best-effort and must NEVER break model training (the platform degrades to the
joblib-on-disk regime that already works).

Env vars (set by docker-compose):
    MLFLOW_TRACKING_URI      — e.g. http://mlflow:5000 (empty = disabled)
    MLFLOW_EXPERIMENT_NAME   — experiment to log runs under (default pfa_student_risk)

Used from models/train_*.py save_with_metadata() functions.
"""
from __future__ import annotations

import logging
import os
from contextlib import contextmanager
from pathlib import Path
from typing import Any, Iterator

logger = logging.getLogger(__name__)

_TRACKING_URI = os.getenv("MLFLOW_TRACKING_URI", "").strip()
_EXPERIMENT = os.getenv("MLFLOW_EXPERIMENT_NAME", "pfa_student_risk")


def is_enabled() -> bool:
    return bool(_TRACKING_URI)


@contextmanager
def start_run(model_name: str) -> Iterator[Any]:
    """
    Context manager that opens an MLflow run named after the model.

    Yields the active run object, or None if MLflow is disabled or unreachable.
    On any failure (import, connection, experiment creation) the body still
    runs — subsequent log_*() calls are no-ops guarded by their own try/except.
    """
    if not _TRACKING_URI:
        yield None
        return
    try:
        import mlflow
        mlflow.set_tracking_uri(_TRACKING_URI)
        mlflow.set_experiment(_EXPERIMENT)
        with mlflow.start_run(run_name=model_name) as run:
            yield run
    except Exception as exc:  # mlflow down, import error, etc. — never fatal
        logger.warning("MLflow unavailable (%s); skipping run logging for %s.", exc, model_name)
        yield None


def log_params(params: dict[str, Any]) -> None:
    if not _TRACKING_URI:
        return
    try:
        import mlflow
        for key, value in params.items():
            if value is None:
                continue
            mlflow.log_param(key, value)
    except Exception as exc:
        logger.warning("mlflow.log_params failed: %s", exc)


def log_metrics(metrics: dict[str, float]) -> None:
    if not _TRACKING_URI:
        return
    try:
        import mlflow
        for key, value in metrics.items():
            if value is None:
                continue
            try:
                mlflow.log_metric(key, float(value))
            except (TypeError, ValueError):
                pass
    except Exception as exc:
        logger.warning("mlflow.log_metrics failed: %s", exc)


def log_tags(tags: dict[str, str]) -> None:
    if not _TRACKING_URI:
        return
    try:
        import mlflow
        for key, value in tags.items():
            if value is None:
                continue
            mlflow.set_tag(key, str(value))
    except Exception as exc:
        logger.warning("mlflow.set_tag failed: %s", exc)


def log_artifact(path: Path) -> None:
    """Logs a single file artifact (e.g. the .joblib model, metadata.json)."""
    if not _TRACKING_URI:
        return
    try:
        import mlflow
        p = Path(path)
        if p.exists() and p.is_file():
            mlflow.log_artifact(str(p))
    except Exception as exc:
        logger.warning("mlflow.log_artifact(%s) failed: %s", path, exc)
