import logging

from fastapi import APIRouter, Depends, Request, HTTPException
from sklearn.pipeline import Pipeline
import pandas as pd

from dependencies import verify_internal_token
from schemas.prediction_schema import PredictionRequest, PredictionResponse

logger = logging.getLogger(__name__)

router = APIRouter(prefix="/predict", tags=["Prediction"])


def _get_risk_model(request: Request) -> Pipeline:
    """
    Pulls the pre-loaded model from app.state.
    Raises 503 if the model was never loaded (startup failed).
    """
    model = getattr(request.app.state, "risk_model", None)
    if model is None:
        raise HTTPException(
            status_code=503,
            detail="Risk model is not loaded. Check startup logs."
        )
    return model


def _score_to_label(probability: float) -> str:
    """
    Maps the raw 0-1 probability to one of the four alert levels
    used by the Alertes table in PFA_DB.

    Thresholds chosen to match typical academic risk definitions:
      < 0.30  -> Faible   (low risk, no action needed)
      < 0.50  -> Moyen    (moderate, worth monitoring)
      < 0.75  -> Eleve    (high, teacher should be notified)
      >= 0.75 -> Critique (critical, immediate intervention)
    """
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
    Predicts the failure-risk probability for a single student.

    Called by the .NET backend's PredictionsController:
        POST /predict
        Body: { "moyenne_generale": 11.5, "taux_absence": 0.25, "nb_modules": 6 }

    Returns:
        { "probabilite": 0.63, "niveau_risque": "Eleve" }
    """
    model: Pipeline = _get_risk_model(request)

    features = pd.DataFrame([{
        "moyenne_generale": payload.moyenne_generale,
        "taux_absence":     payload.taux_absence,
        "nb_modules":       payload.nb_modules,
    }])

    probability = float(model.predict_proba(features)[0][1])

    return PredictionResponse(
        probabilite=round(probability, 4),
        niveau_risque=_score_to_label(probability),
    )


# ─── Batch predictions ────────────────────────────────────────────────────────

@router.post("/batch", response_model=list[PredictionResponse])
def predict_batch(
    payloads: list[PredictionRequest],
    request: Request,
    _: None = Depends(verify_internal_token),
) -> list[PredictionResponse]:
    """
    Predicts failure-risk for multiple students in a single call.

    Called by the .NET backend to assess an entire filière at once:
        POST /predict/batch
        Body: [ { "moyenne_generale": 11.5, ... }, { ... }, ... ]

    Returns a list of PredictionResponse in the same order as input.
    """
    if not payloads:
        return []

    if len(payloads) > 500:
        raise HTTPException(status_code=400, detail="Batch limit is 500 students.")

    model: Pipeline = _get_risk_model(request)

    features = pd.DataFrame([
        {
            "moyenne_generale": p.moyenne_generale,
            "taux_absence":     p.taux_absence,
            "nb_modules":       p.nb_modules,
        }
        for p in payloads
    ])

    probabilities = model.predict_proba(features)[:, 1]

    return [
        PredictionResponse(
            probabilite=round(float(prob), 4),
            niveau_risque=_score_to_label(float(prob)),
        )
        for prob in probabilities
    ]


# ─── Retrain on demand ────────────────────────────────────────────────────────

@router.post("/retrain")
def retrain_models(
    request: Request,
    _: None = Depends(verify_internal_token),
) -> dict:
    """
    Forces retraining of all models from DW data (or synthetic fallback).

    Trains into a sibling staging directory, then atomically swaps each new
    file over the old one (`os.replace`). If training crashes mid-way, the
    previous models stay live and `/predict` keeps serving — instead of being
    bricked with deleted files and no replacements (the old behavior).

    Called by the admin after running ETL sync:
        POST /predict/retrain
        Header: X-Internal-Token: <secret>
    """
    import os
    import shutil
    import joblib
    from models import auto_train, train_risk, train_clustering, train_regression

    saved_dir   = auto_train.SAVED_MODELS_DIR
    staging_dir = saved_dir.parent / "saved_models_staging"

    if staging_dir.exists():
        shutil.rmtree(staging_dir)
    staging_dir.mkdir(parents=True, exist_ok=True)

    # Each train_*.py script writes through its own module-level MODEL_PATH.
    # Redirect all four module-level paths at the staging dir for the duration
    # of this call; restore them afterwards in a finally so a partial run can't
    # leave the modules pointing at staging.
    originals = {
        "auto_train":      auto_train.SAVED_MODELS_DIR,
        "train_risk":      train_risk.MODEL_PATH,
        "train_clustering": train_clustering.MODEL_PATH,
        "train_regression": train_regression.MODEL_PATH,
    }
    try:
        auto_train.SAVED_MODELS_DIR     = staging_dir
        train_risk.MODEL_PATH           = staging_dir / "risk_model.joblib"
        train_clustering.MODEL_PATH     = staging_dir / "cluster_model.joblib"
        train_regression.MODEL_PATH     = staging_dir / "forecast_model.joblib"
        auto_train.ensure_all_models()
    except Exception:
        shutil.rmtree(staging_dir, ignore_errors=True)
        logger.exception("Retraining failed; previous models left in place.")
        raise HTTPException(status_code=500, detail="Retraining failed; previous models still active.")
    finally:
        auto_train.SAVED_MODELS_DIR     = originals["auto_train"]
        train_risk.MODEL_PATH           = originals["train_risk"]
        train_clustering.MODEL_PATH     = originals["train_clustering"]
        train_regression.MODEL_PATH     = originals["train_regression"]

    # Atomically swap each freshly-trained file over the live one.
    # Includes .joblib models plus eval artefacts (.parquet, .json).
    saved_dir.mkdir(parents=True, exist_ok=True)
    for new_file in staging_dir.iterdir():
        if new_file.suffix in (".joblib", ".parquet", ".json"):
            target = saved_dir / new_file.name
            os.replace(new_file, target)        # atomic on same filesystem
            logger.info("Promoted %s into saved_models/.", new_file.name)
    shutil.rmtree(staging_dir, ignore_errors=True)

    # Reload into app.state.
    risk_path     = saved_dir / "risk_model.joblib"
    cluster_path  = saved_dir / "cluster_model.joblib"
    forecast_path = saved_dir / "forecast_model.joblib"

    request.app.state.risk_model     = joblib.load(risk_path)     if risk_path.exists()    else None
    request.app.state.cluster_model  = joblib.load(cluster_path)  if cluster_path.exists() else None
    request.app.state.forecast_model = joblib.load(forecast_path) if forecast_path.exists() else None

    logger.info("All models retrained and reloaded.")

    return {
        "status": "ok",
        "message": "All models retrained and reloaded.",
        "models": {
            "risk_model":     request.app.state.risk_model     is not None,
            "cluster_model":  request.app.state.cluster_model  is not None,
            "forecast_model": request.app.state.forecast_model is not None,
        },
    }
