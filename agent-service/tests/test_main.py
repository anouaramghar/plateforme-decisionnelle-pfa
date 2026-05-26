"""Smoke tests for agent-service startup + /health + /agent/run."""
from __future__ import annotations

import pytest
from fastapi.testclient import TestClient


@pytest.fixture
def client(monkeypatch):
    # Provide the minimal env so Settings() validates without a real NIM key.
    monkeypatch.setenv("AGENT_INTERNAL_TOKEN", "test-internal-token")
    monkeypatch.setenv("NVIDIA_NIM_API_KEY", "nvapi-test")

    # Settings is cached via lru_cache; clear it so each test gets fresh env.
    from config import get_settings
    get_settings.cache_clear()

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


def test_agent_run_requires_internal_token(client):
    r = client.post("/agent/run", json={
        "trace_id": "t1",
        "user_ctx": {"user_id": 1, "role": "Responsable"},
        "messages": [{"role": "user", "content": "hi"}],
    })
    assert r.status_code == 401


def test_agent_run_streams_sse_with_mock_provider(client):
    """Replaces NIMProvider with a Mock so this test does not hit the network."""
    from main import app
    from provider import MockLLMProvider

    app.state.provider = MockLLMProvider(scripted_chunks=["Bon", "jour"])

    r = client.post(
        "/agent/run",
        headers={"X-Internal-Token": "test-internal-token"},
        json={
            "trace_id": "t1",
            "user_ctx": {"user_id": 1, "role": "Responsable"},
            "messages": [{"role": "user", "content": "hi"}],
        },
    )
    assert r.status_code == 200
    assert r.headers["content-type"].startswith("text/event-stream")

    body = r.text  # TestClient collects the whole stream
    # Two token events + one done event, each prefixed by event: + data: lines.
    assert body.count("event: token") == 2
    assert "event: done" in body
    assert '"text": "Bon"' in body
    assert '"text": "jour"' in body
