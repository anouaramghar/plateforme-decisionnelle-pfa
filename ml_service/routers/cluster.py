import logging

from fastapi import APIRouter, Depends, Request, HTTPException
from sklearn.pipeline import Pipeline
import pandas as pd

from dependencies import verify_internal_token
from schemas.prediction_schema import ClusterRequest, ClusterResponse
from features import FEATURE_COLS

logger = logging.getLogger(__name__)

router = APIRouter(prefix="/cluster", tags=["Clustering"])


def _get_cluster_model(request: Request) -> Pipeline:
    model = getattr(request.app.state, "cluster_model", None)
    if model is None:
        raise HTTPException(status_code=503, detail="Cluster model is not loaded.")
    return model


@router.post("", response_model=ClusterResponse)
def assign_cluster(
    payload: ClusterRequest,
    request: Request,
    _: None = Depends(verify_internal_token),
) -> ClusterResponse:
    """
    Assigns a student to one of K pre-trained segments.

    Model now uses 5 features. Backward-compatible: defaults for
    ecart_type_modules (0) and nb_echecs_anterieurs (0).
    """
    model: Pipeline = _get_cluster_model(request)

    features = pd.DataFrame([{
        "moyenne_generale": payload.moyenne_generale,
        "taux_absence":     payload.taux_absence,
        "nb_modules":       payload.nb_modules,
        "ecart_type_modules": payload.ecart_type_modules,
        "nb_echecs_anterieurs": payload.nb_echecs_anterieurs,
    }])[FEATURE_COLS]

    cluster_index = int(model.predict(features)[0])
    return ClusterResponse(cluster=cluster_index)
