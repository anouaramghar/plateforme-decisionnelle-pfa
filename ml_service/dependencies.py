"""Shared FastAPI dependencies for the ML service.

Centralizing the auth check here means a single source of truth for the
shared-secret validation that gates every prediction/cluster/forecast call
from the .NET backend.
"""
from __future__ import annotations

import hmac
import os

from fastapi import Header, HTTPException, status


def verify_internal_token(x_internal_token: str = Header(default="")) -> None:
    """Validates the X-Internal-Token header against ML_INTERNAL_TOKEN.

    Treats a missing/empty server-side token as a fatal misconfiguration
    (503) — never silently allows unauthenticated access. Uses
    hmac.compare_digest to avoid leaking the secret via timing.
    """
    expected = os.getenv("ML_INTERNAL_TOKEN", "")
    if not expected:
        raise HTTPException(
            status_code=status.HTTP_503_SERVICE_UNAVAILABLE,
            detail="ML_INTERNAL_TOKEN is not configured on the ML service.",
        )

    provided = x_internal_token or ""
    if not hmac.compare_digest(expected.encode("utf-8"), provided.encode("utf-8")):
        raise HTTPException(
            status_code=status.HTTP_401_UNAUTHORIZED,
            detail="Unauthorized",
        )
