"""
Executes a validated tool call by POSTing to the backend tool route, forwarding
the user's JWT (L2: the tool runs with the caller's role) and the agent-service
internal token. All failure modes collapse to the {ok:false} envelope so the
agent loop continues gracefully (spec §3.9).
"""
from __future__ import annotations

from typing import Any

import httpx


class ToolExecutor:
    def __init__(self, client: httpx.AsyncClient, internal_token: str):
        self._client = client
        self._internal_token = internal_token

    async def execute(
        self, name: str, args: dict[str, Any], *, jwt: str | None
    ) -> dict[str, Any]:
        if not jwt:
            return {"ok": False, "error": "no JWT in agent context — tool call blocked"}
        headers = {
            "X-Internal-Token": self._internal_token,
            "Authorization": f"Bearer {jwt}",
        }
        try:
            resp = await self._client.post(
                f"/api/copilot/tool/{name}",
                json={"args": args},
                headers=headers,
            )
        except httpx.HTTPError as exc:
            return {"ok": False, "error": f"tool transport error: {type(exc).__name__}"}

        if resp.status_code == 403:
            return {"ok": False, "error": "forbidden"}
        if resp.status_code >= 400:
            return {"ok": False, "error": f"tool http {resp.status_code}"}
        try:
            return resp.json()
        except ValueError:
            return {"ok": False, "error": "tool returned non-JSON"}
