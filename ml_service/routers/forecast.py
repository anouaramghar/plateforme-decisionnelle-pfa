import logging

from fastapi import APIRouter, Depends, Request, HTTPException
from sklearn.pipeline import Pipeline
import numpy as np
import pandas as pd

from dependencies import verify_internal_token
from schemas.prediction_schema import ForecastRequest, ForecastResponse

logger = logging.getLogger(__name__)

router = APIRouter(prefix="/forecast", tags=["Forecast"])

FORECAST_FEATURE_COLS = [
    "moyenne_actuelle", "taux_absence", "nb_modules",
    "ecart_type_modules", "nb_echecs_anterieurs",
]


def _get_forecast_model(request: Request) -> Pipeline:
    model = getattr(request.app.state, "forecast_model", None)
    if model is None:
        raise HTTPException(status_code=503, detail="Forecast model is not loaded.")
    return model


@router.post("", response_model=ForecastResponse)
def forecast_grade(
    payload: ForecastRequest,
    request: Request,
    _: None = Depends(verify_internal_token),
) -> ForecastResponse:
    """
    Predicts a student's final average grade given mid-semester data.

    Model now uses 5 features. Backward-compatible: defaults for
    ecart_type_modules (0) and nb_echecs_anterieurs (0).
    """
    model: Pipeline = _get_forecast_model(request)

    features = pd.DataFrame([{
        "moyenne_actuelle": payload.moyenne_actuelle,
        "taux_absence":     payload.taux_absence,
        "nb_modules":       payload.nb_modules,
        "ecart_type_modules": payload.ecart_type_modules,
        "nb_echecs_anterieurs": payload.nb_echecs_anterieurs,
    }])[FORECAST_FEATURE_COLS]

    raw_prediction = float(model.predict(features)[0])
    note_predite = float(np.clip(raw_prediction, 0.0, 20.0))

    return ForecastResponse(note_predite=round(note_predite, 2))
