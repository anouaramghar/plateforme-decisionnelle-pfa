# agent-service

ENIAD Copilot agent runtime. Backed by NVIDIA NIM (LLaMA 3.3 70B via OpenAI-compatible API).

See:
- `docs/superpowers/specs/2026-05-26-eniad-copilot-design.md` — full design spec
- `docs/superpowers/plans/2026-05-26-eniad-copilot-p1a-foundations.md` — P1.A plan (✅ done)
- `docs/superpowers/plans/2026-05-28-eniad-copilot-p1b-agent-loop.md` — P1.B plan (✅ done)
- `docs/superpowers/plans/2026-05-29-eniad-copilot-p2-list-at-risk.md` — P2 plan (✅ done)

## Current State (2026-05-30)

**Tools available:** `get_student` (by matricule), `list_at_risk` (threshold + filière + niveau)  
**Safety layers done:** L2 (JWT + internal token gate), L3 (pydantic arg validation), L6 (iteration cap)  
**Tests:** 32 passed, 1 skipped (NIM real round-trip, requires `NVIDIA_NIM_API_KEY`)

## Local smoke test

After `docker compose up -d --build` (wait for all 6 services to be `healthy`):

```bash
# 1. Login as admin, capture JWT
TOKEN=$(curl -s -X POST http://localhost/api/auth/login \
  -H "Content-Type: application/json" \
  -d "{\"email\":\"admin@eniad.dz\",\"motDePasse\":\"Admin@ENIAD2025\"}" \
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

## Tool-calling smoke test

```bash
# Ask for a student profile — triggers get_student tool
curl -fsN \
  -H "Authorization: Bearer $TOKEN" \
  -H "Content-Type: application/json" \
  -d '{"message":"Donne-moi le profil de l etudiant E10001"}' \
  http://localhost/api/copilot/chat
```

Expected SSE stream: `event: tool_call` → `event: tool_result` (ok:true) → `event: token` (French summary) → `event: done`.

```bash
# Ask for at-risk students — triggers list_at_risk tool
curl -fsN \
  -H "Authorization: Bearer $TOKEN" \
  -H "Content-Type: application/json" \
  -d '{"message":"Liste-moi les etudiants avec un risque modere ou eleve"}' \
  http://localhost/api/copilot/chat
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

32 tests pass. 1 test (`test_nim_provider_real_round_trip`) is skipped unless `NVIDIA_NIM_API_KEY` is exported.

## SSE Event vocabulary

| Event | Payload | When |
|---|---|---|
| `token` | `{text: str}` | Each LLM token chunk |
| `tool_call` | `{name, args, call_id}` | Before executing a tool |
| `tool_result` | `{name, call_id, ok, summary, error?}` | After tool execution |
| `done` | `{tokens_in, tokens_out, latency_ms}` | Final answer produced |
| `error` | `{message}` | Unrecoverable error or L6 bound hit |
