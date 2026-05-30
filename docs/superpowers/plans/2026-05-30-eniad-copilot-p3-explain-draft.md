# ENIAD Copilot — Phase 3: `explain_risk` + `draft_alert`

> **STATUS: ✅ COMPLETE** — All 4 tasks done on `feat/copilot-p1a-foundations` (2026-05-30). 53 pytest pass + 1 skipped; 31 dotnet tests pass. E2E curl verification pending (needs running stack).

**Scope:** Two new tools — `explain_risk` (Tier-1 read) and `draft_alert` (Tier-2 write + confirm). `query_dw` is intentionally deferred to its own slice (architectural fork unresolved, odbc dependency, untestable in InMemory harness).

**Conventions:** TDD (failing test first), one commit per task, `feat(copilot):` / `test(copilot):`.

---

## Architecture Decisions (pinned before coding)

| Decision | Choice |
|---|---|
| `explain_risk` arg | `matricule: str` (not `student_id: int`) — matches `get_student` pattern |
| `Alerte.Type` at confirm time | `"RisqueEchec"` (hardcoded default in `/api/copilot/confirm`) |
| Severity mapping at confirm | `low→Faible`, `medium→Moyen`, `high→Eleve` |
| `confirm_request` stream position | After last `token`, before `done` |
| `TIER2_TOOLS` approach | `frozenset` in `tools.py`, checked in `agent.py` — keeps loop generic |
| Roles for both tools | `{"Admin", "Responsable"}` (confirm gate makes draft_alert safe for Responsables) |
| `explain_risk` score source | Use latest `PredictionML.ScoreRisque` if present; else heuristic |

---

## File Map

### agent-service (modify)
- `tools.py` — add `EXPLAIN_RISK_TOOL`, `DRAFT_ALERT_TOOL`, `TIER2_TOOLS`
- `tool_args.py` — add `ExplainRiskArgs`, `DraftAlertArgs`
- `agent.py` — emit `confirm_request` for Tier-2 tools that succeed
- `tests/test_tools.py` — 6 new tests
- `tests/test_tool_args.py` — 16 new tests
- `tests/test_agent.py` — 2 new tests

### backend (modify)
- `Controllers/CopilotToolController.cs` — add `explain_risk` + `draft_alert` switch arms + handlers
- `Controllers/CopilotController.cs` — add `POST /api/copilot/confirm` action
- `Tests/Controllers/CopilotToolControllerTests.cs` — 6 new `[Fact]` tests

---

## Task 1 — agent-service: tool schemas + arg models + TIER2_TOOLS

### Step 1.1 — Failing tests for new tools registry

```python
# test_tools.py additions:

def test_explain_risk_tool_is_advertised_to_staff_roles():
    for role in ("Admin", "Responsable"):
        names = [s["function"]["name"] for s in tools_for_role(role)]
        assert "explain_risk" in names

def test_explain_risk_schema_shape():
    spec = next(s for s in tools_for_role("Admin") if s["function"]["name"] == "explain_risk")
    params = spec["function"]["parameters"]
    assert params["required"] == ["matricule"]
    assert params["properties"]["matricule"]["type"] == "string"
    assert params["additionalProperties"] is False

def test_draft_alert_tool_is_advertised_to_staff_roles():
    for role in ("Admin", "Responsable"):
        names = [s["function"]["name"] for s in tools_for_role(role)]
        assert "draft_alert" in names

def test_draft_alert_schema_shape():
    spec = next(s for s in tools_for_role("Admin") if s["function"]["name"] == "draft_alert")
    params = spec["function"]["parameters"]
    assert set(params["required"]) == {"matricule", "severity", "message_fr"}
    assert params["properties"]["severity"]["enum"] == ["low", "medium", "high"]
    assert params["additionalProperties"] is False

def test_tier2_tools_contains_draft_alert():
    from tools import TIER2_TOOLS
    assert "draft_alert" in TIER2_TOOLS
    assert "explain_risk" not in TIER2_TOOLS

def test_etudiant_role_gets_no_new_tools():
    # regression guard
    assert tools_for_role("Etudiant") == []
```

### Step 1.2 — Failing tests for arg models

```python
# test_tool_args.py additions:

# --- explain_risk ---
def test_valid_explain_risk_args():
    ok, result = validate_tool_args("explain_risk", {"matricule": "20231042"})
    assert ok is True and result == {"matricule": "20231042"}

def test_explain_risk_empty_matricule_rejected():
    ok, _ = validate_tool_args("explain_risk", {"matricule": ""})
    assert ok is False

def test_explain_risk_oversized_matricule_rejected():
    ok, _ = validate_tool_args("explain_risk", {"matricule": "x" * 21})
    assert ok is False

def test_explain_risk_extra_arg_rejected():
    ok, _ = validate_tool_args("explain_risk", {"matricule": "x", "email": "a@b"})
    assert ok is False

def test_explain_risk_missing_matricule_rejected():
    ok, _ = validate_tool_args("explain_risk", {})
    assert ok is False

# --- draft_alert ---
def test_valid_draft_alert_args_all_severities():
    for sev in ("low", "medium", "high"):
        ok, result = validate_tool_args("draft_alert",
            {"matricule": "E10001", "severity": sev, "message_fr": "Test message"})
        assert ok is True, f"should accept severity={sev}"
        assert result["severity"] == sev

def test_draft_alert_invalid_severity_rejected():
    for bad in ("critical", "eleve", "Eleve", "HIGH"):
        ok, _ = validate_tool_args("draft_alert",
            {"matricule": "E10001", "severity": bad, "message_fr": "x"})
        assert ok is False, f"should reject severity={bad}"

def test_draft_alert_oversized_message_rejected():
    ok, _ = validate_tool_args("draft_alert",
        {"matricule": "E10001", "severity": "high", "message_fr": "x" * 1001})
    assert ok is False

def test_draft_alert_empty_message_rejected():
    ok, _ = validate_tool_args("draft_alert",
        {"matricule": "E10001", "severity": "high", "message_fr": ""})
    assert ok is False

def test_draft_alert_extra_arg_rejected():
    ok, _ = validate_tool_args("draft_alert",
        {"matricule": "E10001", "severity": "high", "message_fr": "x", "email": "a@b"})
    assert ok is False

def test_draft_alert_missing_matricule_rejected():
    ok, _ = validate_tool_args("draft_alert", {"severity": "high", "message_fr": "x"})
    assert ok is False

def test_draft_alert_missing_severity_rejected():
    ok, _ = validate_tool_args("draft_alert", {"matricule": "E10001", "message_fr": "x"})
    assert ok is False

def test_draft_alert_missing_message_rejected():
    ok, _ = validate_tool_args("draft_alert", {"matricule": "E10001", "severity": "high"})
    assert ok is False

def test_draft_alert_oversized_matricule_rejected():
    ok, _ = validate_tool_args("draft_alert",
        {"matricule": "x" * 21, "severity": "low", "message_fr": "x"})
    assert ok is False
```

### Step 1.3 — Implement in tools.py / tool_args.py

- `- [ ]` Add `EXPLAIN_RISK_TOOL` constant to `tools.py`
- `- [ ]` Add `DRAFT_ALERT_TOOL` constant to `tools.py`
- `- [ ]` Add `TIER2_TOOLS: frozenset[str] = frozenset({"draft_alert"})` to `tools.py`
- `- [ ]` Register both in `TOOL_REGISTRY`
- `- [ ]` Add `ExplainRiskArgs` to `tool_args.py`
- `- [ ]` Add `DraftAlertArgs` to `tool_args.py`
- `- [ ]` Register both in `TOOL_ARG_MODELS`

### Step 1.4 — Run tests, commit
```bash
cd agent-service && python -m pytest tests/test_tools.py tests/test_tool_args.py -q
```
Expected: all pass. Commit: `feat(copilot): agent-service explain_risk + draft_alert tool schemas + L3 arg models`

---

## Task 2 — agent-service: confirm_request loop logic

### Step 2.1 — Failing tests

```python
# test_agent.py additions:

@pytest.mark.asyncio
async def test_draft_alert_success_emits_confirm_request_before_done():
    """After draft_alert succeeds, the loop must emit confirm_request then done."""
    provider = ScriptedLLMProvider(turns=[
        {"tool_call": {"id": "c1", "name": "draft_alert",
                       "arguments": '{"matricule":"E10001","severity":"high","message_fr":"Test alerte."}'}},
        {"text": ["J'ai préparé l'alerte. Confirmez-vous ?"]},
    ])

    async def tool_caller(name, args, *, jwt):
        return {
            "ok": True,
            "data": {
                "draft_id": 7,
                "preview": {"student_name": "Test Etudiant", "severity": "high",
                             "message": "Test alerte."},
                "expires_at": "2026-05-30T18:35:00Z",
            },
        }

    events = [ev async for ev in run_stream(
        _req(), provider=provider, settings=_settings(), tool_caller=tool_caller
    )]
    names = [n for n, _ in events]

    assert "confirm_request" in names
    cr_idx = names.index("confirm_request")
    done_idx = names.index("done")
    assert cr_idx < done_idx, "confirm_request must appear before done"

    cr_payload = dict(events[cr_idx][1])
    assert cr_payload["draft_id"] == 7
    assert cr_payload["tool"] == "draft_alert"


@pytest.mark.asyncio
async def test_draft_alert_failure_does_not_emit_confirm_request():
    """If draft_alert returns ok=False, no confirm_request is emitted."""
    provider = ScriptedLLMProvider(turns=[
        {"tool_call": {"id": "c1", "name": "draft_alert",
                       "arguments": '{"matricule":"NOPE","severity":"high","message_fr":"x"}'}},
        {"text": ["Étudiant introuvable."]},
    ])

    async def tool_caller(name, args, *, jwt):
        return {"ok": False, "error": "étudiant introuvable: NOPE"}

    events = [ev async for ev in run_stream(
        _req(), provider=provider, settings=_settings(), tool_caller=tool_caller
    )]
    names = [n for n, _ in events]
    assert "confirm_request" not in names
    assert names[-1] == "done"
```

### Step 2.2 — Implement in agent.py

Add `ConfirmRequestEvent` to the `from schemas import ...` line.
Add `TIER2_TOOLS` to the `from tools import ...` line.

After the `yield "tool_result"` block, inside the `for call in pending_calls:` loop:

```python
# Emit confirm_request for Tier-2 tools that succeeded (L5 gate).
if name in TIER2_TOOLS and result.get("ok"):
    draft_id = result.get("data", {}).get("draft_id")
    if draft_id is not None:
        yield "confirm_request", ConfirmRequestEvent(
            draft_id=draft_id,
            preview=result.get("data", {}).get("preview", result.get("data", {})),
            tool=name,
        ).model_dump()
```

### Step 2.3 — Run tests, commit
```bash
cd agent-service && python -m pytest tests/test_agent.py -q
cd agent-service && python -m pytest tests/ -q  # full suite
```
Expected: all pass. Commit: `feat(copilot): agent-service emit confirm_request for Tier-2 draft_alert`

---

## Task 3 — backend: explain_risk + draft_alert handlers

### Step 3.1 — Add to SampleData (for draft_alert test)

`SampleData` already has `SeedOne` (student E10001 with notes). No changes needed — `draft_alert` test can use this student.

### Step 3.2 — Failing tests

```csharp
// In CopilotToolControllerTests.cs:

// --- explain_risk ---
[Fact]
public async Task Explain_risk_happy_path_returns_ok_envelope()

[Fact]
public async Task Explain_risk_not_found_returns_ok_false()

// --- draft_alert ---
[Fact]
public async Task Draft_alert_happy_path_inserts_draft_and_returns_draft_id()

[Fact]
public async Task Draft_alert_unknown_matricule_returns_ok_false()

[Fact]
public async Task Draft_alert_invalid_severity_returns_ok_false()

[Fact]
public async Task Draft_alert_missing_message_returns_ok_false()
```

### Step 3.3 — Implement handlers in CopilotToolController.cs

**Constructor additions:**
```csharp
private readonly string? _dwConnString;
// store IConfiguration as field

_dwConnString = config["COPILOT_DW_READONLY_CONN"];
```

**GetUserId helper:**
```csharp
private int? GetUserId() =>
    int.TryParse(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value, out var id)
        ? id : null;
```

**Switch arms:**
```csharp
"explain_risk" => Ok(await ExplainRiskAsync(body.Args, ct)),
"draft_alert"  => Ok(await DraftAlertAsync(body.Args, ct)),
```

**ExplainRiskAsync** — fetches notes, absences, optional ML score for a student by matricule. Returns risk factor decomposition (4 factors: moyenne_basse, absences_elevees, modules_non_valides, tendance_trimestrielle). Risk heuristic identical to GetStudentAsync.

**DraftAlertAsync** — resolves matricule → student, inserts `AlertDraft`, returns `{ok, data: {draft_id, preview, expires_at}}`.

### Step 3.4 — Run tests, commit
```bash
cd backend/PlateformePFA.Tests && dotnet test --nologo
```
Expected: all pass (27+ tests). Commit: `feat(copilot): P3 backend explain_risk + draft_alert handlers`

---

## Task 4 — backend: /api/copilot/confirm endpoint

### Step 4.1 — Implement in CopilotController.cs

```csharp
[HttpPost("confirm")]
[Authorize(Roles = "Admin,Responsable")]
public async Task<IActionResult> ConfirmAsync([FromBody] ConfirmBody body, CancellationToken ct)
```

Logic:
1. Get `userId` from JWT.
2. Load `AlertDraft` where `Id = body.DraftId AND CreatedBy = userId AND Status = "pending_user_confirm" AND ExpiresAt > now`.
3. On fail: `400 {ok: false, error: "draft not found, expired, or not owned by you"}`.
4. Map severity: `low→Faible`, `medium→Moyen`, `high→Eleve`.
5. INSERT `Alerte` with `Type = "RisqueEchec"`, mapped `Niveau`, `EtudiantId = draft.StudentId`, `Message = draft.MessageFr`.
6. UPDATE `AlertDraft.Status = "sent"`, `SentAt = UtcNow`.
7. Save, return `200 {ok: true, data: {alerte_id: newAlerte.Id}}`.

### Step 4.2 — Run tests, commit
Commit: `feat(copilot): POST /api/copilot/confirm — promote AlertDraft to Alerte`

---

## Task 5 — End-to-end verification

- [ ] Rebuild stack: `docker compose up -d --build`
- [ ] curl `explain_risk` — tool_call → tool_result → French summary
- [ ] curl `draft_alert` — tool_call → tool_result → confirm_request → done
- [ ] POST `/api/copilot/confirm {draft_id}` — Alerte row created in DB
- [ ] Full test suites: agent-service `~58 passed, 1 skipped`; backend `27+ passed`
- [ ] Commit: `docs(copilot): P3 E2E smoke test results`

---

## Expected final test counts

| Suite | Before P3 | After P3 |
|---|---|---|
| agent-service pytest | 33 (32 pass + 1 skip) | ~58 (57 pass + 1 skip) |
| backend dotnet test | 21 Fact + 3 Theory | 27 Fact + 3 Theory |
