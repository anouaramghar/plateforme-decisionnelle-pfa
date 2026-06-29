import asyncio
import logging
import time

from fastapi import APIRouter, Depends, Request, HTTPException
from sklearn.pipeline import Pipeline
import pandas as pd
import numpy as np

from dependencies import verify_internal_token
from schemas.prediction_schema import (
    PredictionRequest, PredictionResponse,
    ShapContribution, ExplainResponse,
    GlobalShapResponse,
)
from features import FEATURE_COLS, FEATURE_LABELS

logger = logging.getLogger(__name__)

router = APIRouter(prefix="/predict", tags=["Prediction"])

_retrain_async_lock = asyncio.Lock()
_last_retrain_at: float = 0.0
_RETRAIN_COOLDOWN_S: float = 60.0


def _get_risk_model(request: Request) -> Pipeline:
    model = getattr(request.app.state, "risk_model", None)
    if model is None:
        raise HTTPException(status_code=503, detail="Risk model is not loaded.")
    return model


def _get_chosen_threshold(model: Pipeline) -> float:
    """Returns the optimal threshold stored on the pipeline, or 0.5 as default."""
    return float(getattr(model, "chosen_threshold", 0.5))


def _positive_class_index(request: Request, model: Pipeline) -> int:
    idx = getattr(request.app.state, "risk_positive_class_idx", None)
    if idx is not None:
        return idx
    classes = getattr(model, "classes_", None)
    if classes is None:
        logger.warning("Model has no classes_ attribute; falling back to index 1.")
        return 1
    try:
        idx = int(list(classes).index(1))
    except ValueError:
        logger.warning("Class 1 not found in model.classes_=%s; falling back to index 1.", classes)
        idx = 1
    request.app.state.risk_positive_class_idx = idx
    return idx


def _build_features_df(
    moyenne_generale: float,
    taux_absence: float,
    nb_modules: int,
    ecart_type_modules: float = 0.0,
    nb_echecs_anterieurs: int = 0,
) -> pd.DataFrame:
    return pd.DataFrame([{
        "moyenne_generale": moyenne_generale,
        "taux_absence": taux_absence,
        "nb_modules": nb_modules,
        "ecart_type_modules": ecart_type_modules,
        "nb_echecs_anterieurs": nb_echecs_anterieurs,
    }])[FEATURE_COLS]


def _score_to_label(probability: float) -> str:
    if probability < 0.30:
        return "Faible"
    elif probability < 0.50:
        return "Moyen"
    elif probability < 0.75:
        return "Eleve"
    else:
        return "Critique"


@router.post("", response_model=PredictionResponse)
def predict_risk(
    payload: PredictionRequest,
    request: Request,
    _: None = Depends(verify_internal_token),
) -> PredictionResponse:
    """
    Predicts failure-risk for a single student.

    Uses the model's stored optimal threshold (trained via F2-score optimisation)
    for the binary at-risk classification, while returning the raw probability
    for the frontend to display.
    """
    model: Pipeline = _get_risk_model(request)
    threshold = _get_chosen_threshold(model)

    features = _build_features_df(
        payload.moyenne_generale, payload.taux_absence, payload.nb_modules,
        payload.ecart_type_modules, payload.nb_echecs_anterieurs,
    )

    pos_idx = _positive_class_index(request, model)
    probability = float(model.predict_proba(features)[0][pos_idx])
    at_risk = int(probability >= threshold)

    return PredictionResponse(
        probabilite=round(probability, 4),
        niveau_risque=_score_to_label(probability),
        threshold=round(threshold, 4),
        at_risk=at_risk,
    )


# ─── SHAP explanation (local) ─────────────────────────────────────────────────


@router.post("/explain", response_model=ExplainResponse)
def explain_risk(
    payload: PredictionRequest,
    request: Request,
    _: None = Depends(verify_internal_token),
) -> ExplainResponse:
    """
    Returns SHAP feature contributions for one student's risk prediction.

    POST /predict/explain
    Body: { "moyenne_generale": 9.5, "taux_absence": 0.35, "nb_modules": 6 }
    """
    model: Pipeline = _get_risk_model(request)
    threshold = _get_chosen_threshold(model)

    features = _build_features_df(
        payload.moyenne_generale, payload.taux_absence, payload.nb_modules,
        payload.ecart_type_modules, payload.nb_echecs_anterieurs,
    )

    pos_idx = _positive_class_index(request, model)
    probability = float(model.predict_proba(features)[0][pos_idx])

    try:
        import shap
    except ImportError:
        raise HTTPException(status_code=503, detail="SHAP is not installed.")

    explainer = getattr(request.app.state, "shap_explainer", None)
    if explainer is None:
        classifier = model.named_steps["classifier"]
        explainer = shap.TreeExplainer(classifier)
        request.app.state.shap_explainer = explainer

    scaler = model.named_steps["scaler"]
    X_scaled = pd.DataFrame(scaler.transform(features), columns=FEATURE_COLS)

    shap_values = explainer.shap_values(X_scaled)
    sv = shap_values[0]

    total = sum(abs(v) for v in sv) or 1.0

    contributions = [
        ShapContribution(
            feature=name,
            label=FEATURE_LABELS[name],
            value=round(float(sv[i]), 4),
            pct=round(float(abs(sv[i]) / total), 4),
        )
        for i, name in enumerate(FEATURE_COLS)
    ]

    return ExplainResponse(
        contributions=contributions,
        base_value=round(float(explainer.expected_value), 4),
        probability=round(probability, 4),
    )


# ─── SHAP global summary ──────────────────────────────────────────────────────


@router.get("/shap-summary", response_model=GlobalShapResponse)
def shap_global_summary(
    request: Request,
    _: None = Depends(verify_internal_token),
) -> GlobalShapResponse:
    """
    Returns global SHAP feature importance computed from the training eval set.

    Uses the saved eval_set.parquet to compute mean |SHAP value| per feature
    across all test samples, giving a global picture of which features drive
    risk predictions the most.

    GET /predict/shap-summary
    """
    import json
    from models.auto_train import SAVED_MODELS_DIR

    model: Pipeline = _get_risk_model(request)

    eval_path = SAVED_MODELS_DIR / "eval_set.parquet"
    if not eval_path.exists():
        raise HTTPException(status_code=503, detail="Eval set not found — retrain the model first.")

    try:
        import shap
    except ImportError:
        raise HTTPException(status_code=503, detail="SHAP is not installed.")

    explainer = getattr(request.app.state, "shap_explainer", None)
    if explainer is None:
        classifier = model.named_steps["classifier"]
        explainer = shap.TreeExplainer(classifier)
        request.app.state.shap_explainer = explainer

    eval_df = pd.read_parquet(eval_path)
    X_eval = eval_df[FEATURE_COLS]

    scaler = model.named_steps["scaler"]
    X_scaled = pd.DataFrame(scaler.transform(X_eval), columns=FEATURE_COLS)

    shap_values = explainer.shap_values(X_scaled)

    mean_abs_shap = np.abs(shap_values).mean(axis=0)

    total = mean_abs_shap.sum() or 1.0
    feature_importance = [
        {
            "feature": name,
            "label": FEATURE_LABELS[name],
            "mean_abs_shap": round(float(mean_abs_shap[i]), 4),
            "importance_pct": round(float(mean_abs_shap[i] / total), 4),
        }
        for i, name in enumerate(FEATURE_COLS)
    ]

    feature_importance.sort(key=lambda x: x["mean_abs_shap"], reverse=True)

    return GlobalShapResponse(
        feature_importance=feature_importance,
        n_samples=len(eval_df),
    )


# ─── Batch predictions ────────────────────────────────────────────────────────

@router.post("/batch", response_model=list[PredictionResponse])
def predict_batch(
    payloads: list[PredictionRequest],
    request: Request,
    _: None = Depends(verify_internal_token),
) -> list[PredictionResponse]:
    if not payloads:
        return []

    if len(payloads) > 500:
        raise HTTPException(status_code=400, detail="Batch limit is 500 students.")

    model: Pipeline = _get_risk_model(request)
    threshold = _get_chosen_threshold(model)

    rows = [
        {
            "moyenne_generale": p.moyenne_generale,
            "taux_absence": p.taux_absence,
            "nb_modules": p.nb_modules,
            "ecart_type_modules": p.ecart_type_modules,
            "nb_echecs_anterieurs": p.nb_echecs_anterieurs,
        }
        for p in payloads
    ]
    features = pd.DataFrame(rows)

    pos_idx = _positive_class_index(request, model)
    probabilities = model.predict_proba(features)[:, pos_idx]

    return [
        PredictionResponse(
            probabilite=round(float(prob), 4),
            niveau_risque=_score_to_label(float(prob)),
            threshold=round(threshold, 4),
            at_risk=int(prob >= threshold),
        )
        for prob in probabilities
    ]


# ─── Retrain on demand ────────────────────────────────────────────────────────

@router.post("/retrain")
async def retrain_models(
    request: Request,
    _: None = Depends(verify_internal_token),
) -> dict:
    global _last_retrain_at

    async with _retrain_async_lock:
        now = time.monotonic()
        if now - _last_retrain_at < _RETRAIN_COOLDOWN_S:
            raise HTTPException(status_code=429, detail="Retrain en cours ou trop récent")
        _last_retrain_at = now
        result = await asyncio.to_thread(_do_retrain, request)

    return result


def _do_retrain(request: Request) -> dict:
    import os
    import shutil
    import joblib
    from models import auto_train, train_risk, train_clustering, train_regression

    saved_dir = auto_train.SAVED_MODELS_DIR
    staging_dir = saved_dir / "staging"

    if staging_dir.exists():
        shutil.rmtree(staging_dir)
    staging_dir.mkdir(parents=True, exist_ok=True)

    originals = {
        "auto_train": auto_train.SAVED_MODELS_DIR,
        "train_risk": train_risk.MODEL_PATH,
        "train_clustering": train_clustering.MODEL_PATH,
        "train_regression": train_regression.MODEL_PATH,
    }
    try:
        auto_train.SAVED_MODELS_DIR = staging_dir
        train_risk.MODEL_PATH = staging_dir / "risk_model.joblib"
        train_clustering.MODEL_PATH = staging_dir / "cluster_model.joblib"
        train_regression.MODEL_PATH = staging_dir / "forecast_model.joblib"
        auto_train.ensure_all_models()
    except Exception:
        shutil.rmtree(staging_dir, ignore_errors=True)
        logger.exception("Retraining failed; previous models left in place.")
        raise HTTPException(status_code=500, detail="Retraining failed; previous models still active.")
    finally:
        auto_train.SAVED_MODELS_DIR = originals["auto_train"]
        train_risk.MODEL_PATH = originals["train_risk"]
        train_clustering.MODEL_PATH = originals["train_clustering"]
        train_regression.MODEL_PATH = originals["train_regression"]

    saved_dir.mkdir(parents=True, exist_ok=True)
    for new_file in staging_dir.iterdir():
        if new_file.suffix in (".joblib", ".parquet", ".json", ".png"):
            target = saved_dir / new_file.name
            os.replace(new_file, target)
            logger.info("Promoted %s into saved_models/.", new_file.name)
    shutil.rmtree(staging_dir, ignore_errors=True)

    risk_path = saved_dir / "risk_model.joblib"
    cluster_path = saved_dir / "cluster_model.joblib"
    forecast_path = saved_dir / "forecast_model.joblib"

    request.app.state.risk_model = joblib.load(risk_path) if risk_path.exists() else None
    request.app.state.cluster_model = joblib.load(cluster_path) if cluster_path.exists() else None
    request.app.state.forecast_model = joblib.load(forecast_path) if forecast_path.exists() else None
    request.app.state.risk_positive_class_idx = None
    request.app.state.shap_explainer = None

    logger.info("All models retrained and reloaded.")

    return {
        "status": "ok",
        "message": "All models retrained and reloaded.",
        "models": {
            "risk_model": request.app.state.risk_model is not None,
            "cluster_model": request.app.state.cluster_model is not None,
            "forecast_model": request.app.state.forecast_model is not None,
        },
    }
