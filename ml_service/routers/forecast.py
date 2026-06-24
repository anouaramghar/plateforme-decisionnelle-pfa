import logging

from fastapi import APIRouter, Depends, Request, HTTPException
from sklearn.pipeline import Pipeline
import numpy as np
import pandas as pd

from dependencies import verify_internal_token
from schemas.prediction_schema import ForecastRequest, ForecastResponse

logger = logging.getLogger(__name__)

router = APIRouter(prefix="/forecast", tags=["Forecast"])


def _clamp_nb_modules(nb_modules: int) -> int:
    if nb_modules > 11:
        logger.warning("nb_modules=%d clamped to 11 (training range 3-11)", nb_modules)
        return 11
    return nb_modules


def _get_forecast_model(request: Request) -> Pipeline:
    """Pulls the pre-loaded XGBRegressor pipeline from app.state."""
    model = getattr(request.app.state, "forecast_model", None)
    if model is None:
        raise HTTPException(
            status_code=503,
            detail="Forecast model is not loaded. Check startup logs."
        )
    return model


@router.post("", response_model=ForecastResponse)
def forecast_grade(
    payload: ForecastRequest,
    request: Request,
    _: None = Depends(verify_internal_token),
) -> ForecastResponse:
    """
    Predicts a student's final average grade given mid-semester data.

    Called by the .NET backend's analytics views:
        POST /forecast
        Body: { "moyenne_actuelle": 13.5, "taux_absence": 0.10, "nb_modules": 6 }

    Returns:
        { "note_predite": 12.3 }

    The returned grade is clipped to [0, 20] — XGBoost regressors can
    occasionally predict slightly outside the training range.
    """
    model: Pipeline = _get_forecast_model(request)

    features = pd.DataFrame([{
        "moyenne_actuelle": payload.moyenne_actuelle,
        "taux_absence":     payload.taux_absence,
        "nb_modules":       _clamp_nb_modules(payload.nb_modules),
    }])

    # predict() returns a 1D numpy array — we take index [0].
    raw_prediction = float(model.predict(features)[0])

    # Clip to valid grade range: XGBoost can extrapolate outside [0, 20].
    note_predite = float(np.clip(raw_prediction, 0.0, 20.0))

    return ForecastResponse(note_predite=round(note_predite, 2))
