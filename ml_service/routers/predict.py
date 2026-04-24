import os
import logging

from fastapi import APIRouter, Depends, Request, HTTPException, Header
from sklearn.pipeline import Pipeline
import pandas as pd

from schemas.prediction_schema import PredictionRequest, PredictionResponse

logger = logging.getLogger(__name__)

router = APIRouter(prefix="/predict", tags=["Prediction"])


def verify_token(x_internal_token: str = Header(default="")) -> None:
    token = os.getenv("ML_INTERNAL_TOKEN")
    if token and x_internal_token != token:
        raise HTTPException(status_code=401, detail="Unauthorized")


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
    _: None = Depends(verify_token),
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
    _: None = Depends(verify_token),
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
    _: None = Depends(verify_token),
) -> dict:
    """
    Forces retraining of all models from DW data (or synthetic fallback).

    Deletes existing .joblib files so auto_train rebuilds them from scratch,
    then reloads the new models into app.state.

    Called by the admin after running ETL sync:
        POST /predict/retrain
        Header: X-Internal-Token: <secret>
    """
    import joblib
    from pathlib import Path
    from models.auto_train import ensure_all_models, SAVED_MODELS_DIR

    # Delete existing models to force retraining
    for model_file in SAVED_MODELS_DIR.glob("*.joblib"):
        model_file.unlink()
        logger.info("Deleted %s for retraining.", model_file.name)

    # Retrain all models
    ensure_all_models()

    # Reload into app.state
    risk_path    = SAVED_MODELS_DIR / "risk_model.joblib"
    cluster_path = SAVED_MODELS_DIR / "cluster_model.joblib"
    forecast_path = SAVED_MODELS_DIR / "forecast_model.joblib"

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
        }
    }
