"""
Tests for GET /metrics endpoint.
"""
from __future__ import annotations

import json
import os
import sys
from pathlib import Path

import numpy as np
import pandas as pd
import pytest

_ML_SERVICE_ROOT = Path(__file__).parent.parent
if str(_ML_SERVICE_ROOT) not in sys.path:
    sys.path.insert(0, str(_ML_SERVICE_ROOT))


# ─── Helpers ──────────────────────────────────────────────────────────────────

_GOOD_TOKEN = "test-token-abc123"

def patch_metrics_paths(monkeypatch, target_dir):
    import routers.metrics as m
    monkeypatch.setattr(m, "_SAVED_DIR", target_dir)
    monkeypatch.setattr(m, "_EVAL_PARQUET", target_dir / "eval_set.parquet")
    monkeypatch.setattr(m, "_METADATA_JSON", target_dir / "metadata.json")
    monkeypatch.setattr(m, "_FORECAST_EVAL_PARQUET", target_dir / "forecast_eval_set.parquet")
    monkeypatch.setattr(m, "_FORECAST_METADATA_JSON", target_dir / "forecast_metadata.json")


# ─── Fixtures ─────────────────────────────────────────────────────────────────

@pytest.fixture(autouse=True)
def _set_ml_token(monkeypatch):
    """Ensures ML_INTERNAL_TOKEN is always set so lifespan does not raise."""
    monkeypatch.setenv("ML_INTERNAL_TOKEN", _GOOD_TOKEN)


@pytest.fixture()
def client(monkeypatch, tmp_path):
    """
    Returns a TestClient whose /metrics endpoint points at tmp_path for
    saved_models artefacts, keeping the real saved_models/ directory untouched.
    """
    patch_metrics_paths(monkeypatch, tmp_path)

    from fastapi.testclient import TestClient
    import main as main_mod

    with TestClient(main_mod.app, raise_server_exceptions=True) as c:
        yield c


@pytest.fixture()
def trained_artefacts(tmp_path):
    """
    Trains a tiny real model, writes eval_set.parquet + metadata.json into
    tmp_path, and returns the tmp_path for assertions.
    """
    from models.train_risk import generate_data, train, save_with_metadata

    df = generate_data(200, seed=0)
    pipeline, X_test, y_test = train(df)
    save_with_metadata(pipeline, X_test, y_test, models_dir=tmp_path)
    return tmp_path


# ─── Tests ────────────────────────────────────────────────────────────────────

class TestMetricsHappyPath:
    """With eval_set.parquet present, source should be 'computed'."""

    def test_all_required_fields_present(self, client, monkeypatch, tmp_path, trained_artefacts):
        # Point the metrics module at the tmp_path that has trained artefacts.
        patch_metrics_paths(monkeypatch, trained_artefacts)

        resp = client.get("/metrics", headers={"X-Internal-Token": _GOOD_TOKEN})
        assert resp.status_code == 200, resp.text
        data = resp.json()

        required_keys = {"auc", "f1", "precision", "recall", "n_samples",
                         "model_version", "trained_at", "source", "risk", "forecast"}
        assert required_keys.issubset(set(data.keys())), f"Missing keys: {required_keys - set(data.keys())}"

        # Provenance contract
        assert "data_source" in data["risk"]
        assert "split_strategy" in data["risk"]
        assert data["risk"]["data_source"] in {"dw", "synthetic", "unknown"}
        assert data["risk"]["split_strategy"] in {"out_of_time", "grouped_student", "unknown"}

        assert "data_source" in data["forecast"]
        assert "split_strategy" in data["forecast"]
        assert data["forecast"]["data_source"] in {"dw", "synthetic", "unknown"}
        assert data["forecast"]["split_strategy"] in {"out_of_time", "grouped_student", "unknown"}

    def test_auc_in_range(self, client, monkeypatch, trained_artefacts):
        patch_metrics_paths(monkeypatch, trained_artefacts)

        resp = client.get("/metrics", headers={"X-Internal-Token": _GOOD_TOKEN})
        data = resp.json()
        assert 0.0 <= data["auc"] <= 1.0, f"AUC out of range: {data['auc']}"

    def test_source_is_computed_when_parquet_exists(self, client, monkeypatch, trained_artefacts):
        patch_metrics_paths(monkeypatch, trained_artefacts)

        resp = client.get("/metrics", headers={"X-Internal-Token": _GOOD_TOKEN})
        assert resp.json()["source"] == "computed"

    def test_n_samples_positive(self, client, monkeypatch, trained_artefacts):
        patch_metrics_paths(monkeypatch, trained_artefacts)

        resp = client.get("/metrics", headers={"X-Internal-Token": _GOOD_TOKEN})
        assert resp.json()["n_samples"] > 0


class TestMetricsFallback:
    """Without eval_set.parquet, source should be 'metadata'."""

    def test_source_is_metadata_when_no_parquet(self, client, monkeypatch, tmp_path, trained_artefacts):
        # Write only metadata.json (no parquet).
        meta_only = tmp_path / "meta_only"
        meta_only.mkdir()
        (meta_only / "metadata.json").write_text(
            (trained_artefacts / "metadata.json").read_text()
        )
        # Also copy joblib so the model exists but parquet is absent.
        import shutil
        shutil.copy(trained_artefacts / "risk_model.joblib", meta_only / "risk_model.joblib")

        patch_metrics_paths(monkeypatch, meta_only)

        resp = client.get("/metrics", headers={"X-Internal-Token": _GOOD_TOKEN})
        assert resp.status_code == 200
        assert resp.json()["source"] == "metadata"

    def test_no_500_when_no_files_at_all(self, client, monkeypatch, tmp_path):
        empty_dir = tmp_path / "empty"
        empty_dir.mkdir()

        patch_metrics_paths(monkeypatch, empty_dir)

        resp = client.get("/metrics", headers={"X-Internal-Token": _GOOD_TOKEN})
        assert resp.status_code == 200
        data = resp.json()
        assert data["source"] == "metadata"
        assert data["auc"] == 0.0


class TestMetricsAuth:
    """Auth boundary: missing or wrong token → 401/503."""

    def test_401_when_token_missing(self, client):
        resp = client.get("/metrics")
        assert resp.status_code in (401, 422), resp.text

    def test_401_when_token_wrong(self, client):
        resp = client.get("/metrics", headers={"X-Internal-Token": "wrong-token"})
        assert resp.status_code == 401, resp.text

    def test_200_when_token_correct(self, client):
        resp = client.get("/metrics", headers={"X-Internal-Token": _GOOD_TOKEN})
        assert resp.status_code == 200, resp.text
