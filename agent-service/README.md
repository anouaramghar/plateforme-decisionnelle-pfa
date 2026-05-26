# agent-service

ENIAD Copilot agent runtime. See `docs/superpowers/specs/2026-05-26-eniad-copilot-design.md` for the full design and `docs/superpowers/plans/2026-05-26-eniad-copilot-p1a-foundations.md` for the P1.A implementation plan.

## Local smoke test

After `docker compose up -d --build` (wait for all 6 services to be `healthy`):

```bash
# 1. Login as admin, capture JWT
TOKEN=$(curl -s -X POST http://localhost/api/auth/login \
  -H "Content-Type: application/json" \
  -d "{\"email\":\"admin@eniad.ma\",\"motDePasse\":\"$ADMIN_SEED_PASSWORD\"}" \
  | python -c "import sys,json; print(json.load(sys.stdin)['token'])")

# 2. Stream a chat turn
curl -fsN \
  -H "Authorization: Bearer $TOKEN" \
  -H "Content-Type: application/json" \
  -d '{"message":"Bonjour"}' \
  http://localhost/api/copilot/chat
```

Expected output:

```
event: token
data: {"text": "Bonjour"}

event: done
data: {"tokens_in": 0, "tokens_out": 0, "latency_ms": 2473}
```

## Network topology

`agent-service` is on **two** Docker networks:

- `pfa_internal_ml` — how backend reaches it (no internet exposure).
- `pfa_public` — required so it can reach `https://integrate.api.nvidia.com/v1`. `pfa_internal_ml` is `internal: true` which blocks outbound. No host port is bound and no nginx route forwards to it, so this does NOT expose it externally.

## Tests

```bash
cd agent-service
python -m pytest tests/ -v
```

10 tests pass. The 11th (`test_nim_provider_real_round_trip`) is skipped unless `NVIDIA_NIM_API_KEY` is exported.
