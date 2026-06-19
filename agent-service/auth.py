"""Shared-secret middleware — same pattern as ml_service."""
from __future__ import annotations

import hmac

from fastapi import Header, HTTPException, status

from config import get_settings


async def require_internal_token(
    x_internal_token: str | None = Header(default=None, alias="X-Internal-Token"),
) -> None:
    expected = get_settings().internal_token
    # Constant-time compare to avoid leaking the token via response-time
    # side-channels. Negligible risk over a docker bridge network, but
    # cheap defense-in-depth.
    if not x_internal_token or not hmac.compare_digest(x_internal_token, expected):
        raise HTTPException(
            status_code=status.HTTP_401_UNAUTHORIZED,
            detail="Invalid or missing X-Internal-Token",
        )
