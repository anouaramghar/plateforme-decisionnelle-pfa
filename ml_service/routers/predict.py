import os

from fastapi import APIRouter, Depends, Request, HTTPException, Header
from sklearn.pipeline import Pipeline
import pandas as pd

from schemas.prediction_schema import PredictionRequest, PredictionResponse

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
