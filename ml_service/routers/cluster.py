import os

from fastapi import APIRouter, Depends, Request, HTTPException, Header
from sklearn.pipeline import Pipeline
import pandas as pd

from schemas.prediction_schema import ClusterRequest, ClusterResponse

router = APIRouter(prefix="/cluster", tags=["Clustering"])


def verify_token(x_internal_token: str = Header(default="")) -> None:
    token = os.getenv("ML_INTERNAL_TOKEN")
    if token and x_internal_token != token:
        raise HTTPException(status_code=401, detail="Unauthorized")


def _get_cluster_model(request: Request) -> Pipeline:
    """Pulls the pre-loaded K-Means pipeline from app.state."""
    model = getattr(request.app.state, "cluster_model", None)
    if model is None:
        raise HTTPException(
            status_code=503,
            detail="Cluster model is not loaded. Check startup logs."
        )
    return model


@router.post("", response_model=ClusterResponse)
def assign_cluster(
    payload: ClusterRequest,
    request: Request,
    _: None = Depends(verify_token),
) -> ClusterResponse:
    """
    Assigns a student to one of K pre-trained segments.

    Called by the .NET backend when building student segmentation dashboards.
        POST /cluster
        Body: { "moyenne_generale": 14.0, "taux_absence": 0.05, "nb_modules": 7 }

    Returns:
        { "cluster": 2 }

    The cluster index is 0-based (0 to K-1).
    The backend maps cluster → label (e.g., "Top performers") using its own
    lookup table — keeping the ML service stateless.
    """
    model: Pipeline = _get_cluster_model(request)

    features = pd.DataFrame([{
        "moyenne_generale": payload.moyenne_generale,
        "taux_absence":     payload.taux_absence,
        "nb_modules":       payload.nb_modules,
    }])

    # KMeans.predict() returns the cluster index as an int array.
    # We take index [0] for the single row.
    cluster_index = int(model.predict(features)[0])

    return ClusterResponse(cluster=cluster_index)
