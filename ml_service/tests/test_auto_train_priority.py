from __future__ import annotations

import sys
from pathlib import Path

import pandas as pd

_ML_SERVICE_ROOT = Path(__file__).parent.parent
if str(_ML_SERVICE_ROOT) not in sys.path:
    sys.path.insert(0, str(_ML_SERVICE_ROOT))


def test_risk_training_prefers_dw_over_uci(monkeypatch, tmp_path):
    import data.db_loader as db_loader
    import data.uci_loader as uci_loader
    import models.auto_train as auto_train
    import models.train_risk as train_risk

    dw_df = pd.DataFrame({"source": ["dw"]})
    saved = {}

    monkeypatch.setattr(auto_train, "SAVED_MODELS_DIR", tmp_path)
    monkeypatch.setattr(db_loader, "load_risk_data", lambda: dw_df)
    monkeypatch.setattr(
        uci_loader,
        "load_uci_risk_data",
        lambda: (_ for _ in ()).throw(AssertionError("UCI should be fallback only")),
    )
    monkeypatch.setattr(train_risk, "train", lambda df: ("pipeline", "X", "y", {"chosen_threshold": 0.5}))
    monkeypatch.setattr(
        train_risk,
        "save_with_metadata",
        lambda *_args, **kwargs: saved.update(data_source=kwargs["data_source"]),
    )

    auto_train._ensure_risk_model()

    assert saved["data_source"] == "dw"


def test_forecast_training_prefers_dw_over_uci(monkeypatch, tmp_path):
    import data.db_loader as db_loader
    import data.uci_loader as uci_loader
    import models.auto_train as auto_train
    import models.train_regression as train_regression

    dw_df = pd.DataFrame({"source": ["dw"]})
    saved = {}

    monkeypatch.setattr(auto_train, "SAVED_MODELS_DIR", tmp_path)
    monkeypatch.setattr(db_loader, "load_forecast_data", lambda: dw_df)
    monkeypatch.setattr(
        uci_loader,
        "load_uci_forecast_data",
        lambda: (_ for _ in ()).throw(AssertionError("UCI should be fallback only")),
    )
    monkeypatch.setattr(train_regression, "train", lambda df: ("pipeline", "X", "y"))
    monkeypatch.setattr(
        train_regression,
        "save_with_metadata",
        lambda *_args, **kwargs: saved.update(data_source=kwargs["data_source"]),
    )

    auto_train._ensure_forecast_model()

    assert saved["data_source"] == "dw"


def test_cluster_training_prefers_dw_over_uci(monkeypatch, tmp_path):
    import data.db_loader as db_loader
    import data.uci_loader as uci_loader
    import models.auto_train as auto_train
    import models.train_clustering as train_clustering

    dw_df = pd.DataFrame({"source": ["dw"]})
    saved = {}

    monkeypatch.setattr(auto_train, "SAVED_MODELS_DIR", tmp_path)
    monkeypatch.setattr(db_loader, "load_clustering_data", lambda: dw_df)
    monkeypatch.setattr(
        uci_loader,
        "load_uci_clustering_data",
        lambda: (_ for _ in ()).throw(AssertionError("UCI should be fallback only")),
    )
    monkeypatch.setattr(train_clustering, "train", lambda df, k: "pipeline")
    monkeypatch.setattr(
        train_clustering,
        "save_with_metadata",
        lambda *_args, **kwargs: saved.update(n_samples=kwargs["n_samples"]),
    )

    auto_train._ensure_cluster_model()

    assert saved["n_samples"] == 1


def test_dw_loader_accepts_existing_copilot_dw_env(monkeypatch):
    import data.db_loader as db_loader

    monkeypatch.delenv("DW_CONNECTION_STRING", raising=False)
    monkeypatch.setenv("COPILOT_DW_READONLY_CONN", "Server=local;Database=PFA_DW;")

    assert db_loader._get_connection_string().endswith("Server=local;Database=PFA_DW;")


def test_dw_loader_adds_default_odbc_driver(monkeypatch):
    import data.db_loader as db_loader

    monkeypatch.setenv("DW_CONNECTION_STRING", "Server=local;Database=PFA_DW;")

    assert db_loader._get_connection_string().startswith("Driver={ODBC Driver 18 for SQL Server};")


def test_dw_loader_normalizes_trust_server_certificate(monkeypatch):
    import data.db_loader as db_loader

    monkeypatch.setenv(
        "DW_CONNECTION_STRING",
        "Driver={ODBC Driver 18 for SQL Server};Server=local;TrustServerCertificate=True;",
    )

    assert "TrustServerCertificate=yes" in db_loader._get_connection_string()
