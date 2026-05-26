"""Smoke tests for agent-service startup + /health."""
from __future__ import annotations

import pytest
from fastapi.testclient import TestClient


@pytest.fixture
def client(monkeypatch):
    # Provide the minimal env so Settings() validates without a real NIM key.
    monkeypatch.setenv("ML_INTERNAL_TOKEN", "test-internal-token")
    monkeypatch.setenv("NVIDIA_NIM_API_KEY", "nvapi-test")

    # Import inside the fixture so env vars are present before module load.
    from main import app

    with TestClient(app) as c:
        yield c


def test_health_returns_ok_when_settings_load(client):
    r = client.get("/health")
    assert r.status_code == 200
    body = r.json()
    assert body["status"] == "ok"
    assert body["service"] == "agent-service"
