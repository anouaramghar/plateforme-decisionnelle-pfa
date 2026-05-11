"""
Tests for GET /metrics endpoint.

Covers:
  - Happy path: all required fields present, AUC in [0, 1].
  - source="computed" when eval_set.parquet exists.
  - source="metadata" when only metadata.json exists (no parquet).
  - 401 when X-Internal-Token header is missing.
  - 401 when X-Internal-Token header has a wrong value.

All tests monkeypatch ML_INTERNAL_TOKEN so the lifespan check passes without
a real environment variable set, and use a tmp_path fixture to write test
artefacts without touching the real saved_models/ directory.
"""
from __future__ import annotations

import json
import os
import sys
from pathlib import Path

import numpy as np
import pandas as pd
import pytest

# ─── Make sure the ml_service root is on sys.path so imports work
# (when pytest is run from /app inside the container, cwd is already /app,
# but being explicit avoids confusion when running locally too).
_ML_SERVICE_ROOT = Path(__file__).parent.parent
if str(_ML_SERVICE_ROOT) not in sys.path:
    sys.path.insert(0, str(_ML_SERVICE_ROOT))


# ─── Fixtures ─────────────────────────────────────────────────────────────────

_GOOD_TOKEN = "test-token-abc123"


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
    # Redirect the module-level paths inside routers.metrics BEFORE importing
    # (or re-importing after monkeypatching).
    import routers.metrics as metrics_mod

    monkeypatch.setattr(metrics_mod, "_SAVED_DIR", tmp_path)
    monkeypatch.setattr(metrics_mod, "_EVAL_PARQUET", tmp_path / "eval_set.parquet")
    monkeypatch.setattr(metrics_mod, "_METADATA_JSON", tmp_path / "metadata.json")

    # Import app AFTER env is patched — lifespan reads ML_INTERNAL_TOKEN at
    # startup, but TestClient triggers the lifespan on __enter__.
    # We import lazily to ensure the monkeypatch above is already in place.
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
        import routers.metrics as m
        monkeypatch.setattr(m, "_SAVED_DIR", trained_artefacts)
        monkeypatch.setattr(m, "_EVAL_PARQUET", trained_artefacts / "eval_set.parquet")
        monkeypatch.setattr(m, "_METADATA_JSON", trained_artefacts / "metadata.json")

        resp = client.get("/metrics", headers={"X-Internal-Token": _GOOD_TOKEN})
        assert resp.status_code == 200, resp.text
        data = resp.json()

        required_keys = {"auc", "f1", "precision", "recall", "n_samples",
                         "model_version", "trained_at", "source"}
        assert required_keys == set(data.keys()), f"Missing keys: {required_keys - set(data.keys())}"

    def test_auc_in_range(self, client, monkeypatch, trained_artefacts):
        import routers.metrics as m
        monkeypatch.setattr(m, "_SAVED_DIR", trained_artefacts)
        monkeypatch.setattr(m, "_EVAL_PARQUET", trained_artefacts / "eval_set.parquet")
        monkeypatch.setattr(m, "_METADATA_JSON", trained_artefacts / "metadata.json")

        resp = client.get("/metrics", headers={"X-Internal-Token": _GOOD_TOKEN})
        data = resp.json()
        assert 0.0 <= data["auc"] <= 1.0, f"AUC out of range: {data['auc']}"

    def test_source_is_computed_when_parquet_exists(self, client, monkeypatch, trained_artefacts):
        import routers.metrics as m
        monkeypatch.setattr(m, "_SAVED_DIR", trained_artefacts)
        monkeypatch.setattr(m, "_EVAL_PARQUET", trained_artefacts / "eval_set.parquet")
        monkeypatch.setattr(m, "_METADATA_JSON", trained_artefacts / "metadata.json")

        resp = client.get("/metrics", headers={"X-Internal-Token": _GOOD_TOKEN})
        assert resp.json()["source"] == "computed"

    def test_n_samples_positive(self, client, monkeypatch, trained_artefacts):
        import routers.metrics as m
        monkeypatch.setattr(m, "_SAVED_DIR", trained_artefacts)
        monkeypatch.setattr(m, "_EVAL_PARQUET", trained_artefacts / "eval_set.parquet")
        monkeypatch.setattr(m, "_METADATA_JSON", trained_artefacts / "metadata.json")

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

        import routers.metrics as m
        monkeypatch.setattr(m, "_SAVED_DIR", meta_only)
        monkeypatch.setattr(m, "_EVAL_PARQUET", meta_only / "eval_set.parquet")
        monkeypatch.setattr(m, "_METADATA_JSON", meta_only / "metadata.json")

        resp = client.get("/metrics", headers={"X-Internal-Token": _GOOD_TOKEN})
        assert resp.status_code == 200
        assert resp.json()["source"] == "metadata"

    def test_no_500_when_no_files_at_all(self, client, monkeypatch, tmp_path):
        empty_dir = tmp_path / "empty"
        empty_dir.mkdir()

        import routers.metrics as m
        monkeypatch.setattr(m, "_SAVED_DIR", empty_dir)
        monkeypatch.setattr(m, "_EVAL_PARQUET", empty_dir / "eval_set.parquet")
        monkeypatch.setattr(m, "_METADATA_JSON", empty_dir / "metadata.json")

        resp = client.get("/metrics", headers={"X-Internal-Token": _GOOD_TOKEN})
        assert resp.status_code == 200
        data = resp.json()
        assert data["source"] == "metadata"
        # Zeros are acceptable when there's nothing to read from.
        assert data["auc"] == 0.0


class TestMetricsAuth:
    """Auth boundary: missing or wrong token → 401/503."""

    def test_401_when_token_missing(self, client):
        resp = client.get("/metrics")
        # FastAPI returns 422 for missing required header, but our dependency
        # uses Header(default="") so empty string triggers 401 from hmac check.
        assert resp.status_code in (401, 422), resp.text

    def test_401_when_token_wrong(self, client):
        resp = client.get("/metrics", headers={"X-Internal-Token": "wrong-token"})
        assert resp.status_code == 401, resp.text

    def test_200_when_token_correct(self, client):
        resp = client.get("/metrics", headers={"X-Internal-Token": _GOOD_TOKEN})
        assert resp.status_code == 200, resp.text
