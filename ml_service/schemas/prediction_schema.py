from pydantic import BaseModel, Field


class PredictionRequest(BaseModel):
    """Input features sent by the .NET backend for a single student."""

    moyenne_generale: float = Field(..., ge=0, le=20,
        description="Average grade across all modules (0–20 scale)")
    taux_absence: float = Field(..., ge=0, le=1,
        description="Absence rate as a fraction of total hours (0 = never absent, 1 = always absent)")
    nb_modules: int = Field(..., ge=1, le=30,
        description="Number of modules the student is enrolled in")


class PredictionResponse(BaseModel):
    """What the ML service returns to the backend."""

    probabilite: float = Field(..., ge=0, le=1,
        description="Probability of failure risk (0 = low risk, 1 = high risk)")
    label: str = Field(...,
        description="Human-readable risk level: Faible / Moyen / Eleve / Critique")


class ClusterRequest(BaseModel):
    """Input for student segmentation (clustering)."""

    moyenne_generale: float = Field(..., ge=0, le=20)
    taux_absence: float = Field(..., ge=0, le=1)
    nb_modules: int = Field(..., ge=1, le=30)


class ClusterResponse(BaseModel):
    """Which cluster/segment the student belongs to."""

    cluster: int = Field(..., ge=0,
        description="Cluster index assigned by the model")


class ForecastRequest(BaseModel):
    """Input for grade forecasting (regression)."""

    moyenne_actuelle: float = Field(..., ge=0, le=20,
        description="Current average grade this semester")
    taux_absence: float = Field(..., ge=0, le=1)
    nb_modules: int = Field(..., ge=1, le=30)


class ForecastResponse(BaseModel):
    """Predicted final grade."""

    note_predite: float = Field(..., ge=0, le=20,
        description="Predicted final average grade")
