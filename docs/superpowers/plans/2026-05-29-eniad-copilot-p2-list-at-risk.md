# ENIAD Copilot — Phase 2: `list_at_risk` Second Read Tool

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add a second Tier-1 read tool — `list_at_risk` — that lets the LLM retrieve the full list of at-risk students filtered by risk threshold, filière, and niveau. The agent loop, SSE plumbing, and safety architecture are untouched; this plan is purely additive. Proven end-to-end with `curl` against the rebuilt stack.

**Architecture:** Identical pattern to `get_student` (P1.B). Only four files change:

- **Backend:** one new handler arm inside the existing `CopilotToolController` switch-case + its tests.
- **agent-service:** one new entry in `tools.py` (JSON Schema) and one new pydantic model in `tool_args.py`. `agent.py`, `tool_executor.py`, `main.py` are **not touched** — the loop is generic.

**Tech Stack:** Same as P1.B. No new dependencies.

**Reference spec:** `docs/superpowers/specs/2026-05-26-eniad-copilot-design.md` §2.1 (`list_at_risk` Tier-1 tool). Safety layers L2 (JWT + internal token, already in the controller) and L3 (pydantic, `extra='forbid'`) apply as before. L4/L5/L7/L8, `query_dw`, Tier-2/3 tools, and the React Copilot panel are **out of scope**.

---

## Tool Contract

```
list_at_risk(threshold: float, filiere?: string, niveau?: string)
  → {ok: true, data: {students: [{matricule, nom, prenom, filiere, niveau,
                                   moyenne_generale, total_absences_h,
                                   score_risque, risque}],
                       total: int, cap_applied: bool}}
  | {ok: false, error: str, hint?: str}
```

- `threshold` required, 0.0–1.0: only students with `score_risque >= threshold` returned.
- `filiere` optional, max 20 chars: filter by filière code (case-insensitive).
- `niveau` optional, max 10 chars: filter by niveau (e.g. `"CI1"`, `"CI2"`).
- Result capped at **50 rows** (advisor guidance), ordered by `score_risque DESC`.
- `cap_applied: true` tells the model there may be more, so it can ask the user to narrow the filter.
- Risk heuristic: identical to `get_student` — inline, self-contained.

---

## File Structure

### backend (modify)
- `backend/PlateformePFA.API/Controllers/CopilotToolController.cs` — add `"list_at_risk"` arm + `ListAtRiskAsync` handler
- `backend/PlateformePFA.Tests/Controllers/CopilotToolControllerTests.cs` — add `list_at_risk` test group

### agent-service (modify)
- `agent-service/tools.py` — add `LIST_AT_RISK_TOOL` + registry entry
- `agent-service/tool_args.py` — add `ListAtRiskArgs` pydantic model

### agent-service (modify — tests only)
- `agent-service/tests/test_tools.py` — extend role-filter + schema tests
- `agent-service/tests/test_tool_args.py` — extend with `list_at_risk` arg tests

---

## Conventions

Same as P1.B: TDD (failing test first), one commit per task, Conventional Commits `feat(copilot):` / `test(copilot):`.

---

## Pre-flight

- [ ] **Step 0.1: Confirm E2E baseline passes after backend rebuild**

```bash
# PowerShell
$ADMIN_EMAIL = (Select-String "^ADMIN_SEED_EMAIL=" .env | ForEach-Object { $_.Line.Split("=",2)[1] })
$ADMIN_PASS  = (Select-String "^ADMIN_SEED_PASSWORD=" .env | ForEach-Object { $_.Line.Split("=",2)[1] })
$TOKEN = (Invoke-RestMethod -Method Post -Uri "http://localhost/api/auth/login" `
    -ContentType "application/json" `
    -Body "{`"email`":`"$ADMIN_EMAIL`",`"motDePasse`":`"$ADMIN_PASS`"}").token

# Should show: event: tool_call → event: tool_result (ok:true) → event: done
curl -fsN -X POST http://localhost/api/copilot/chat `
    -H "Authorization: Bearer $TOKEN" `
    -H "Content-Type: application/json" `
    -d '{"message":"Donne-moi le profil de l etudiant E10001"}' | Select-String "event:|ok"
```

Expected: `event: tool_call`, then `event: tool_result` with `"ok":true` containing `"matricule":"E10001"` and `"risque":"faible"`, then `event: done`.

- [ ] **Step 0.2: Confirm both test suites are still green**

```bash
cd agent-service && python -m pytest tests/ -q && cd ..
cd backend/PlateformePFA.Tests && dotnet test --nologo && cd ../..
```

Expected: agent-service `25 passed, 1 skipped`; backend `17 passed`.

---

## Task 1: Backend — `list_at_risk` handler

**Files:**
- Modify: `backend/PlateformePFA.API/Controllers/CopilotToolController.cs`
- Modify: `backend/PlateformePFA.Tests/Controllers/CopilotToolControllerTests.cs`

- [ ] **Step 1.1: Add a high-risk student fixture to `SampleData`**

Extend `backend/PlateformePFA.Tests/Fixtures/SampleData.cs` with a new `SeedHighRisk` method so `list_at_risk` tests have students at different risk levels:

```csharp
/// <summary>
/// Seeds a second student with low grades (moy 6.0) and many absences (40h)
/// so list_at_risk tests can assert on threshold filtering.
/// Requires SeedOne to have been called first (shares the same Filiere + Module).
/// Idempotent.
/// </summary>
public static Etudiant SeedHighRisk(AppDbContext ctx)
{
    var etu = ctx.Etudiants.FirstOrDefault(e => e.Matricule == "E10002");
    if (etu != null) return etu;

    var fil = ctx.Filieres.First(f => f.Code == "GI");
    var mod = ctx.Modules.First(m => m.Code == "GI01");

    etu = new Etudiant
    {
        Matricule = "E10002", Nom = "AtRisque", Prenom = "High",
        FiliereId = fil.Id, Niveau = "CI1", Annee = "2025/2026",
    };
    ctx.Etudiants.Add(etu);
    ctx.SaveChanges();

    ctx.Notes.Add(new Note
    {
        EtudiantId = etu.Id, ModuleId = mod.Id,
        NoteTD = 5m, NoteTP = 5m, NoteExamen = 6m, NoteFinal = 6.0m,
        Annee = "2025/2026", Semestre = "S2",
    });
    ctx.Absences.Add(new Absence
    {
        EtudiantId = etu.Id, ModuleId = mod.Id,
        NombreHeures = 40, DateAbsence = DateTime.UtcNow.AddDays(-10),
    });
    ctx.SaveChanges();

    return etu;
}
```

> Note: if `Absence` model has different field names in your schema, adjust accordingly — check `Models/Absence.cs`.

- [ ] **Step 1.2: Write the failing tests for `list_at_risk`**

Append to `backend/PlateformePFA.Tests/Controllers/CopilotToolControllerTests.cs`, inside the class, after the existing tests:

```csharp
// ── list_at_risk tests ──────────────────────────────────────────────────────

[Fact]
public async Task List_at_risk_threshold_zero_returns_all_students()
{
    using (var ctx = _factory.CreateContext()) SampleData.SeedHighRisk(ctx);

    var client = await AuthedClientAsync();
    client.DefaultRequestHeaders.Add("X-Internal-Token", InternalToken);

    var res = await client.PostAsJsonAsync(
        "/api/copilot/tool/list_at_risk",
        new { args = new { threshold = 0.0 } });

    res.StatusCode.Should().Be(HttpStatusCode.OK);
    var body = await res.Content.ReadAsStringAsync();
    body.Should().Contain("\"ok\":true");
    body.Should().Contain("\"students\"");
    // Both seeded students (E10001 faible + E10002 eleve) should appear.
    body.Should().Contain("E10001");
    body.Should().Contain("E10002");
}

[Fact]
public async Task List_at_risk_high_threshold_filters_out_low_risk_students()
{
    using (var ctx = _factory.CreateContext()) SampleData.SeedHighRisk(ctx);

    var client = await AuthedClientAsync();
    client.DefaultRequestHeaders.Add("X-Internal-Token", InternalToken);

    // 0.60 threshold: only the high-risk student (E10002, moy 6.0 + 40h abs)
    // should be returned. E10001 (moy 12.2, 0 abs, score ~0.12) must be absent.
    var res = await client.PostAsJsonAsync(
        "/api/copilot/tool/list_at_risk",
        new { args = new { threshold = 0.60 } });

    res.StatusCode.Should().Be(HttpStatusCode.OK);
    var body = await res.Content.ReadAsStringAsync();
    body.Should().Contain("\"ok\":true");
    body.Should().Contain("E10002");
    body.Should().NotContain("E10001");
}

[Fact]
public async Task List_at_risk_filiere_filter_scopes_results()
{
    using (var ctx = _factory.CreateContext()) SampleData.SeedHighRisk(ctx);

    var client = await AuthedClientAsync();
    client.DefaultRequestHeaders.Add("X-Internal-Token", InternalToken);

    var res = await client.PostAsJsonAsync(
        "/api/copilot/tool/list_at_risk",
        new { args = new { threshold = 0.0, filiere = "GI" } });

    res.StatusCode.Should().Be(HttpStatusCode.OK);
    var body = await res.Content.ReadAsStringAsync();
    body.Should().Contain("\"ok\":true");
    body.Should().Contain("\"students\"");
}

[Fact]
public async Task List_at_risk_invalid_threshold_returns_ok_false()
{
    var client = await AuthedClientAsync();
    client.DefaultRequestHeaders.Add("X-Internal-Token", InternalToken);

    // threshold out of range
    var res = await client.PostAsJsonAsync(
        "/api/copilot/tool/list_at_risk",
        new { args = new { threshold = 1.5 } });

    res.StatusCode.Should().Be(HttpStatusCode.OK);
    (await res.Content.ReadAsStringAsync()).Should().Contain("\"ok\":false");
}
```

- [ ] **Step 1.3: Run to verify tests fail**

```bash
cd backend/PlateformePFA.Tests && dotnet test --filter "List_at_risk" --nologo && cd ../..
```

Expected: tests fail / 404 response (handler doesn't exist yet).

- [ ] **Step 1.4: Implement `ListAtRiskAsync` + add switch-case arm**

In `CopilotToolController.cs`:

1. Add `"list_at_risk"` to the switch expression:
```csharp
"list_at_risk" => Ok(await ListAtRiskAsync(body.Args, ct)),
```

2. Add the handler method after `GetStudentAsync`:

```csharp
private async Task<object> ListAtRiskAsync(JsonElement args, CancellationToken ct)
{
    // --- parse args ---
    if (args.ValueKind != JsonValueKind.Object ||
        !args.TryGetProperty("threshold", out var threshEl) ||
        threshEl.ValueKind != JsonValueKind.Number ||
        !threshEl.TryGetDouble(out var threshDouble) ||
        threshDouble < 0.0 || threshDouble > 1.0)
    {
        return new { ok = false, error = "threshold must be a number between 0.0 and 1.0" };
    }
    var threshold = (decimal)threshDouble;

    string? filiereFilter = null;
    if (args.TryGetProperty("filiere", out var filEl) &&
        filEl.ValueKind == JsonValueKind.String)
    {
        filiereFilter = filEl.GetString()?.Trim().ToUpperInvariant();
    }

    string? niveauFilter = null;
    if (args.TryGetProperty("niveau", out var nivEl) &&
        nivEl.ValueKind == JsonValueKind.String)
    {
        niveauFilter = nivEl.GetString()?.Trim();
    }

    // --- query ---
    const int Cap = 50;

    var query = _db.Etudiants
        .AsNoTracking()
        .Where(e =>
            (filiereFilter == null || (e.Filiere != null && e.Filiere.Code.ToUpper() == filiereFilter)) &&
            (niveauFilter  == null || e.Niveau == niveauFilter))
        .Select(e => new
        {
            e.Matricule,
            e.Nom,
            e.Prenom,
            e.Niveau,
            Filiere    = e.Filiere != null ? e.Filiere.Code : "",
            Moyenne    = (decimal?)e.Notes!
                .Where(n => n.NoteFinal.HasValue)
                .Average(n => n.NoteFinal),
            AbsencesH  = (int?)e.Absences!.Sum(a => a.NombreHeures) ?? 0,
        });

    var rows = await query.ToListAsync(ct);

    // compute risk inline, filter by threshold, sort, cap
    var result = rows
        .Select(s =>
        {
            var moy  = s.Moyenne ?? 0m;
            decimal risk = (moy < 10m ? 0.55m : moy < 12m ? 0.32m : 0.12m)
                         + (s.AbsencesH > 30 ? 0.22m : s.AbsencesH > 18 ? 0.12m : 0m);
            risk = Math.Min(0.98m, risk);
            var bucket = risk >= 0.65m ? "eleve" : risk >= 0.35m ? "modere" : "faible";
            return new
            {
                s.Matricule,
                nom              = s.Nom,
                prenom           = s.Prenom,
                filiere          = s.Filiere,
                niveau           = s.Niveau,
                moyenne_generale = Math.Round(moy, 2),
                total_absences_h = s.AbsencesH,
                score_risque     = Math.Round(risk, 3),
                risque           = bucket,
            };
        })
        .Where(s => s.score_risque >= threshold)
        .OrderByDescending(s => s.score_risque)
        .ToList();

    var total      = result.Count;
    var capApplied = total > Cap;
    var students   = result.Take(Cap).ToList();

    return new
    {
        ok   = true,
        data = new
        {
            students,
            total,
            cap_applied = capApplied,
        },
    };
}
```

- [ ] **Step 1.5: Run the tests — expect pass**

```bash
cd backend/PlateformePFA.Tests && dotnet test --filter "List_at_risk" --nologo && cd ../..
```

Expected: `Passed! ... Total: 4`.

- [ ] **Step 1.6: Verify Absence model fields** *(if Step 1.1 failed on `Absence` fields)*

If `SeedHighRisk` failed to compile because the `Absence` model doesn't have a `DateAbsence` field or uses different names, check `Models/Absence.cs` and adjust the fixture accordingly. The fixture can omit optional fields — only `EtudiantId`, `ModuleId`, `NombreHeures` are strictly required for the risk heuristic.

- [ ] **Step 1.7: Run full backend suite — no regressions**

```bash
cd backend/PlateformePFA.Tests && dotnet test --nologo && cd ../..
```

Expected: `Passed! ... Total: 21` (17 existing + 4 new).

- [ ] **Step 1.8: Commit**

```bash
git add backend/PlateformePFA.API/Controllers/CopilotToolController.cs `
       backend/PlateformePFA.Tests/Controllers/CopilotToolControllerTests.cs `
       backend/PlateformePFA.Tests/Fixtures/SampleData.cs
git commit -m "feat(copilot): P2 backend list_at_risk tool (threshold + filiere + niveau filter)"
```

---

## Task 2: agent-service — register `list_at_risk`

**Files:**
- Modify: `agent-service/tools.py`
- Modify: `agent-service/tool_args.py`
- Modify: `agent-service/tests/test_tools.py`
- Modify: `agent-service/tests/test_tool_args.py`

Nothing in `agent.py`, `tool_executor.py`, or `main.py` changes.

- [ ] **Step 2.1: Add failing tests for `list_at_risk` in the registry**

Append to `agent-service/tests/test_tools.py`:

```python
def test_list_at_risk_tool_is_advertised_to_staff_roles():
    from tools import tools_for_role

    for role in ("Admin", "Responsable"):
        names = [s["function"]["name"] for s in tools_for_role(role)]
        assert "list_at_risk" in names


def test_list_at_risk_schema_shape():
    from tools import tools_for_role

    spec = next(
        s for s in tools_for_role("Admin")
        if s["function"]["name"] == "list_at_risk"
    )
    params = spec["function"]["parameters"]
    assert "threshold" in params["required"]
    assert params["properties"]["threshold"]["type"] == "number"
    # filiere and niveau are optional
    assert "filiere" in params["properties"]
    assert "niveau"  in params["properties"]
    assert params["additionalProperties"] is False
```

Append to `agent-service/tests/test_tool_args.py`:

```python
def test_valid_list_at_risk_args_minimal():
    from tool_args import validate_tool_args

    ok, result = validate_tool_args("list_at_risk", {"threshold": 0.5})
    assert ok is True
    assert result["threshold"] == 0.5
    assert "filiere" not in result or result.get("filiere") is None


def test_valid_list_at_risk_args_full():
    from tool_args import validate_tool_args

    ok, result = validate_tool_args(
        "list_at_risk", {"threshold": 0.65, "filiere": "GI", "niveau": "CI1"}
    )
    assert ok is True
    assert result == {"threshold": 0.65, "filiere": "GI", "niveau": "CI1"}


def test_list_at_risk_threshold_out_of_range_rejected():
    from tool_args import validate_tool_args

    for bad in (-0.1, 1.1, 2.0):
        ok, _ = validate_tool_args("list_at_risk", {"threshold": bad})
        assert ok is False, f"expected rejection for threshold={bad}"


def test_list_at_risk_extra_arg_rejected():
    from tool_args import validate_tool_args

    ok, _ = validate_tool_args("list_at_risk", {"threshold": 0.5, "hack": "x"})
    assert ok is False


def test_list_at_risk_missing_threshold_rejected():
    from tool_args import validate_tool_args

    ok, _ = validate_tool_args("list_at_risk", {})
    assert ok is False
```

- [ ] **Step 2.2: Run to verify failures**

```bash
cd agent-service && python -m pytest tests/test_tools.py tests/test_tool_args.py -q && cd ..
```

Expected: 2 new tests in `test_tools.py` fail; 5 new in `test_tool_args.py` fail.

- [ ] **Step 2.3: Add `LIST_AT_RISK_TOOL` to `agent-service/tools.py`**

Add after `GET_STUDENT_TOOL`:

```python
LIST_AT_RISK_TOOL: dict[str, Any] = {
    "type": "function",
    "function": {
        "name": "list_at_risk",
        "description": (
            "Retourne la liste des étudiants dont le score de risque d'échec est "
            "supérieur ou égal au seuil donné. Résultats triés par risque décroissant, "
            "limités à 50 étudiants."
        ),
        "parameters": {
            "type": "object",
            "properties": {
                "threshold": {
                    "type": "number",
                    "minimum": 0.0,
                    "maximum": 1.0,
                    "description": (
                        "Seuil de score de risque (0.0–1.0). "
                        "0.35 = risque modéré+, 0.65 = risque élevé."
                    ),
                },
                "filiere": {
                    "type": "string",
                    "description": "Code filière (ex: 'GI', 'IRSI'). Optionnel.",
                },
                "niveau": {
                    "type": "string",
                    "description": "Niveau (ex: 'CI1', 'CI2'). Optionnel.",
                },
            },
            "required": ["threshold"],
            "additionalProperties": False,
        },
    },
}
```

Add `"list_at_risk"` to `TOOL_REGISTRY`:

```python
TOOL_REGISTRY: dict[str, dict[str, Any]] = {
    "get_student":  {"spec": GET_STUDENT_TOOL,   "roles": {"Admin", "Responsable"}},
    "list_at_risk": {"spec": LIST_AT_RISK_TOOL,  "roles": {"Admin", "Responsable"}},
}
```

- [ ] **Step 2.4: Add `ListAtRiskArgs` to `agent-service/tool_args.py`**

Add after `GetStudentArgs`:

```python
class ListAtRiskArgs(BaseModel):
    model_config = {"extra": "forbid"}
    threshold: float = Field(ge=0.0, le=1.0)
    filiere: str | None = Field(default=None, max_length=20)
    niveau:  str | None = Field(default=None, max_length=10)
```

Add to `TOOL_ARG_MODELS`:

```python
TOOL_ARG_MODELS: dict[str, type[BaseModel]] = {
    "get_student":  GetStudentArgs,
    "list_at_risk": ListAtRiskArgs,
}
```

- [ ] **Step 2.5: Run to verify tests pass**

```bash
cd agent-service && python -m pytest tests/test_tools.py tests/test_tool_args.py -q && cd ..
```

Expected: all pass.

- [ ] **Step 2.6: Run full agent-service suite — no regressions**

```bash
cd agent-service && python -m pytest tests/ -q && cd ..
```

Expected: `32 passed, 1 skipped` (25 + 7 new).

- [ ] **Step 2.7: Commit**

```bash
git add agent-service/tools.py agent-service/tool_args.py `
       agent-service/tests/test_tools.py agent-service/tests/test_tool_args.py
git commit -m "feat(copilot): agent-service list_at_risk tool schema + L3 arg validation"
```

---

## Task 3: End-to-end verification

- [ ] **Step 3.1: Rebuild agent-service (new tools.py / tool_args.py must be in the image)**

```bash
docker compose build --no-cache agent-service
docker compose up -d agent-service
# wait for healthy
```

- [ ] **Step 3.2: Full regression sweep**

```bash
cd agent-service && python -m pytest tests/ -q && cd ..
cd backend/PlateformePFA.Tests && dotnet test --nologo && cd ../..
```

Expected: agent-service `32 passed, 1 skipped`; backend `21 passed`.

- [ ] **Step 3.3: E2E curl — list at-risk students**

```bash
# Get a token first (see Step 0.1 for PowerShell variant)
TOKEN=$(curl -s -X POST http://localhost/api/auth/login \
  -H "Content-Type: application/json" \
  -d "{\"email\":\"$ADMIN_SEED_EMAIL\",\"motDePasse\":\"$ADMIN_SEED_PASSWORD\"}" \
  | python -c "import sys,json; print(json.load(sys.stdin)['token'])")

curl -fsN -X POST http://localhost/api/copilot/chat \
  -H "Authorization: Bearer $TOKEN" \
  -H "Content-Type: application/json" \
  -d '{"message":"Liste-moi les etudiants avec un risque modere ou eleve"}'
```

Expected SSE stream contains, in order: `event: tool_call` with `"name":"list_at_risk"`, then `event: tool_result` with `"ok":true`, then `event: token` (French summary), then `event: done`.

- [ ] **Step 3.4: Confirm the tool route is still L2-gated**

```bash
curl -s -o /dev/null -w "%{http_code}\n" \
  -X POST http://localhost/api/copilot/tool/list_at_risk \
  -H "Authorization: Bearer $TOKEN" \
  -H "Content-Type: application/json" \
  -d '{"args":{"threshold":0.0}}'
```

Expected: `401` (valid JWT but no internal token → rejected).

---

## Self-Review Notes

- **No loop changes:** `agent.py`, `tool_executor.py`, `main.py` should be **untouched**. If you edited them, something went wrong.
- **EF InMemory safety:** the `Average()` projection uses `(decimal?)` nullable cast — mandatory because InMemory throws if the sequence is empty.
- **Cap:** 50 rows enforced in the handler, not in the DB query — the full list is fetched then filtered in memory (acceptable because the total student count is bounded by the school's actual enrollment, not internet-scale).
- **Known follow-ups (not this plan):** `query_dw` (NL→SQL + L4 — its own Phase 3 plan), `explain_risk`, React Copilot panel, Tier-2 `draft_alert`.
