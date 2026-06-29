import numpy as np

from routers.predict import predict_batch
from schemas.prediction_schema import PredictionRequest


class FakeModel:
    classes_ = np.array([0, 1])
    chosen_threshold = 0.5

    def predict_proba(self, features):
        assert list(features.columns) == [
            "moyenne_generale",
            "taux_absence",
            "nb_modules",
            "ecart_type_modules",
            "nb_echecs_anterieurs",
        ]
        assert features.iloc[0]["ecart_type_modules"] == 0.0
        assert features.iloc[0]["nb_echecs_anterieurs"] == 0
        return np.array([[0.8, 0.2]])


class FakeState:
    risk_model = FakeModel()
    risk_positive_class_idx = None


class FakeRequest:
    class App:
        state = FakeState()

    app = App()


def test_prediction_request_accepts_three_feature_payload():
    payload = PredictionRequest(moyenne_generale=12, taux_absence=0.1, nb_modules=6)

    assert payload.ecart_type_modules == 0.0
    assert payload.nb_echecs_anterieurs == 0


def test_predict_batch_uses_full_feature_contract():
    result = predict_batch(
        [PredictionRequest(moyenne_generale=12, taux_absence=0.1, nb_modules=6)],
        FakeRequest(),
        None,
    )

    assert result[0].probabilite == 0.2
    assert result[0].niveau_risque == "Faible"
