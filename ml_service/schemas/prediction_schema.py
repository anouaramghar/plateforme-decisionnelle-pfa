from pydantic import BaseModel, Field


class PredictionRequest(BaseModel):
    """Input features sent by the .NET backend for a single student."""

    moyenne_generale: float = Field(..., ge=0, le=20,
        description="Average grade across all modules (0–20 scale)")
    taux_absence: float = Field(..., ge=0, le=1,
        description="Absence rate as a fraction of total hours (0 = never absent, 1 = always absent)")
    nb_modules: int = Field(..., ge=1, le=30,
        description="Number of modules enrolled in. NOTE: model trained on data up to 11 modules; values above 11 are extrapolations.")


class PredictionResponse(BaseModel):
    """What the ML service returns to the backend."""

    probabilite: float = Field(..., ge=0, le=1,
        description="Probability of failure risk (0 = low risk, 1 = high risk)")
    niveau_risque: str = Field(...,
        description="Human-readable risk level: Faible / Moyen / Eleve / Critique")


class ClusterRequest(BaseModel):
    """Input for student segmentation (clustering)."""

    moyenne_generale: float = Field(..., ge=0, le=20)
    taux_absence: float = Field(..., ge=0, le=1)
    nb_modules: int = Field(..., ge=1, le=30,
        description="Number of modules enrolled in. NOTE: model trained on data up to 11 modules; values above 11 are extrapolations.")


class ClusterResponse(BaseModel):
    """Which cluster/segment the student belongs to."""

    cluster: int = Field(..., ge=0,
        description="Cluster index assigned by the model")


class ForecastRequest(BaseModel):
    """Input for grade forecasting (regression)."""

    moyenne_actuelle: float = Field(..., ge=0, le=20,
        description="Current average grade this semester")
    taux_absence: float = Field(..., ge=0, le=1)
    nb_modules: int = Field(..., ge=1, le=30,
        description="Number of modules enrolled in. NOTE: model trained on data up to 11 modules; values above 11 are extrapolations.")


class ForecastResponse(BaseModel):
    """Predicted final grade."""

    note_predite: float = Field(..., ge=0, le=20,
        description="Predicted final average grade")


class ShapContribution(BaseModel):
    feature: str = Field(..., description="Feature key: moyenne_generale | taux_absence | nb_modules")
    label: str   = Field(..., description="Human-readable French label")
    value: float = Field(..., description="Raw SHAP value in log-odds space. Positive = increases risk.")
    pct: float   = Field(..., ge=0, le=1, description="Fraction of total absolute SHAP mass for this feature")


class ExplainResponse(BaseModel):
    contributions: list[ShapContribution]
    base_value: float  = Field(..., description="SHAP base value (expected model log-odds)")
    probability: float = Field(..., ge=0, le=1, description="Model predicted risk probability")
