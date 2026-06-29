import logging

from pydantic import BaseModel, Field, field_validator

_logger = logging.getLogger(__name__)


def _warn_nb_modules_above_training_range(value: int) -> int:
    if 11 < value <= 30:
        _logger.warning("nb_modules=%d exceeds training range 3-11 (schema cap 30)", value)
    return value


class PredictionRequest(BaseModel):
    """Input features sent by the .NET backend for a single student."""

    moyenne_generale: float = Field(..., ge=0, le=20)
    taux_absence: float = Field(..., ge=0, le=1)
    nb_modules: int = Field(..., ge=1, le=30)
    ecart_type_modules: float = Field(default=0.0, ge=0, le=20)
    nb_echecs_anterieurs: int = Field(default=0, ge=0, le=30)

    @field_validator("nb_modules")
    @classmethod
    def _warn_nb_modules(cls, v: int) -> int:
        return _warn_nb_modules_above_training_range(v)


class PredictionResponse(BaseModel):
    """Response from the ML service — includes raw probability and optimal threshold."""

    probabilite: float = Field(..., ge=0, le=1)
    niveau_risque: str = Field(...)
    threshold: float = Field(default=0.5, ge=0, le=1)
    at_risk: int = Field(default=0, ge=0, le=1)


class ClusterRequest(BaseModel):
    moyenne_generale: float = Field(..., ge=0, le=20)
    taux_absence: float = Field(..., ge=0, le=1)
    nb_modules: int = Field(..., ge=1, le=30)
    ecart_type_modules: float = Field(default=0.0, ge=0, le=20)
    nb_echecs_anterieurs: int = Field(default=0, ge=0, le=30)

    @field_validator("nb_modules")
    @classmethod
    def _warn_nb_modules(cls, v: int) -> int:
        return _warn_nb_modules_above_training_range(v)


class ClusterResponse(BaseModel):
    cluster: int = Field(..., ge=0)


class ForecastRequest(BaseModel):
    moyenne_actuelle: float = Field(..., ge=0, le=20)
    taux_absence: float = Field(..., ge=0, le=1)
    nb_modules: int = Field(..., ge=1, le=30)
    ecart_type_modules: float = Field(default=0.0, ge=0, le=20)
    nb_echecs_anterieurs: int = Field(default=0, ge=0, le=30)

    @field_validator("nb_modules")
    @classmethod
    def _warn_nb_modules(cls, v: int) -> int:
        return _warn_nb_modules_above_training_range(v)


class ForecastResponse(BaseModel):
    note_predite: float = Field(..., ge=0, le=20)


class ShapContribution(BaseModel):
    feature: str = Field(...)
    label: str = Field(...)
    value: float = Field(...)
    pct: float = Field(..., ge=0, le=1)


class ExplainResponse(BaseModel):
    contributions: list[ShapContribution]
    base_value: float = Field(...)
    probability: float = Field(..., ge=0, le=1)


class GlobalShapImportance(BaseModel):
    feature: str = Field(...)
    label: str = Field(...)
    mean_abs_shap: float = Field(...)
    importance_pct: float = Field(..., ge=0, le=1)


class GlobalShapResponse(BaseModel):
    feature_importance: list[GlobalShapImportance]
    n_samples: int = Field(...)
