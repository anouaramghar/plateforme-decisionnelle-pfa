"""Backend tool callback client."""
from __future__ import annotations

import json

import httpx
import pytest


def _client(handler) -> httpx.AsyncClient:
    return httpx.AsyncClient(
        transport=httpx.MockTransport(handler),
        base_url="http://backend:8080",
    )


@pytest.mark.asyncio
async def test_execute_forwards_jwt_and_internal_token_and_returns_envelope():
    seen = {}

    def handler(request: httpx.Request) -> httpx.Response:
        seen["url"] = str(request.url)
        seen["auth"] = request.headers.get("Authorization")
        seen["internal"] = request.headers.get("X-Internal-Token")
        seen["body"] = json.loads(request.content)
        return httpx.Response(200, json={"ok": True, "data": {"matricule": "42"}})

    from tool_executor import ToolExecutor

    async with _client(handler) as c:
        ex = ToolExecutor(c, internal_token="sekret")
        result = await ex.execute("get_student", {"matricule": "42"}, jwt="jwt-abc")

    assert result == {"ok": True, "data": {"matricule": "42"}}
    assert seen["url"] == "http://backend:8080/api/copilot/tool/get_student"
    assert seen["auth"] == "Bearer jwt-abc"
    assert seen["internal"] == "sekret"
    assert seen["body"] == {"args": {"matricule": "42"}}


@pytest.mark.asyncio
async def test_execute_maps_403_to_forbidden_envelope():
    def handler(request: httpx.Request) -> httpx.Response:
        return httpx.Response(403)

    from tool_executor import ToolExecutor

    async with _client(handler) as c:
        ex = ToolExecutor(c, internal_token="sekret")
        result = await ex.execute("get_student", {"matricule": "42"}, jwt="x")

    assert result == {"ok": False, "error": "forbidden"}


@pytest.mark.asyncio
async def test_execute_maps_transport_error_to_envelope():
    def handler(request: httpx.Request) -> httpx.Response:
        raise httpx.ConnectError("backend down")

    from tool_executor import ToolExecutor

    async with _client(handler) as c:
        ex = ToolExecutor(c, internal_token="sekret")
        result = await ex.execute("get_student", {"matricule": "42"}, jwt="x")

    assert result["ok"] is False
    assert "transport" in result["error"]
