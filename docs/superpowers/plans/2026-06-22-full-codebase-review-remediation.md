# Full Codebase Review Remediation Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Resolve every blocker and correctness, authorization, ML-validity, Copilot, frontend-state, database, ETL, and portability issue identified by the 2026-06-22 three-agent codebase review.

**Architecture:** Preserve the existing ASP.NET controller/service, React/TanStack Query, FastAPI/scikit-learn, SQL Server, and CopilotKit boundaries. Introduce small policy and contract helpers where multiple endpoints need the same invariant, make failure states explicit instead of converting them into valid business values, and secure the Copilot stream with a short-lived HTTP-only session cookie. Execute the work in dependency order so each task leaves a buildable, testable checkpoint.

**Tech Stack:** ASP.NET Core 8, EF Core 8, SQL Server 2022, xUnit, React 18, TypeScript 5, TanStack Query 5, Vitest, CopilotKit Runtime v2, Express, FastAPI, Pydantic 2, pandas, scikit-learn, XGBoost, pytest, Docker Compose, nginx.

---

## Review baseline and scope

The plan targets the current `main` worktree, including staged user changes. Before implementation, preserve or commit those changes in an isolated worktree. Do not discard either side of the unresolved frontend merge without comparing the intended Enseignant and Predictions behavior.

Baseline observed on 2026-06-22:

- Backend: 46 tests pass.
- Frontend: 8 unit tests pass, but production build and lint fail on merge markers.
- ML service: 9 tests pass with deprecation warnings.
- Copilot runtime: TypeScript compilation passes.
- Docker Compose configuration validates.

## File map

### Backend authorization and integrity

- `backend/PlateformePFA.API/Services/AcademicAccessService.cs`: new centralized teacher/module/student scope checks.
- `backend/PlateformePFA.API/Controllers/NotesController.cs`: enforce teacher assignment, student/module compatibility, and optimistic concurrency.
- `backend/PlateformePFA.API/Controllers/AbsencesController.cs`: same enforcement for absences and alert refresh after every mutation.
- `backend/PlateformePFA.API/Controllers/RapportsController.cs`: restrict teacher report generation and downloads.
- `backend/PlateformePFA.API/Services/ReportGenerator.cs`: accept an explicit caller scope rather than treating omitted filters as institution-wide permission.
- `backend/PlateformePFA.API/Services/AuthService.cs`: atomic refresh-token consumption.
- `backend/PlateformePFA.API/Services/AlerteService.cs`: resolve alerts when their triggering condition disappears.
- `backend/PlateformePFA.API/Controllers/PredictionsController.cs`: reject malformed ML responses, preserve unavailable status without a numeric score, and scope features by academic period.
- `backend/PlateformePFA.API/Services/RiskScorer.cs`: select only successful predictions.
- `backend/PlateformePFA.API/Controllers/CopilotSessionController.cs`: issue and clear short-lived Copilot session cookies.
- `backend/PlateformePFA.API/Controllers/CopilotToolController.cs`: confirm/cancel real alert drafts.
- `backend/PlateformePFA.API/Controllers/AdminController.cs`: one transaction and one synchronization lock around ETL.
- `backend/PlateformePFA.API/Models/Note.cs`, `Models/Presence.cs`: row-version concurrency tokens.
- `backend/PlateformePFA.API/Data/AppDbContext.cs`, `Data/RuntimeMigrations.cs`: concurrency and supporting database constraints.

### Frontend and Copilot

- `frontend/src/pages/Enseignant.tsx`, `frontend/src/pages/Predictions.tsx`: resolve merge conflicts first.
- `frontend/src/context/AuthContext.tsx`: clear user-scoped caches on login and create/revoke Copilot sessions.
- `frontend/src/layout/CommandPalette.tsx`, `layout/Topbar.tsx`: correct response contract and role-filter actions.
- `frontend/src/pages/Students.tsx`, `components/copilot/ConfirmCard.tsx`: use the single server-side alert-draft contract.
- `frontend/src/pages/Alerts.tsx`: invalidate all alert badge caches.
- `copilot-runtime/src/auth.ts`: new cookie extraction and JWT verification helper.
- `copilot-runtime/src/index.ts`: require a verified session for inference and bind it to tool execution.
- `copilot-runtime/src/tools.ts`: one canonical `draft_alert` schema and authenticated forwarding.

### ML, database, and deployment

- `ml_service/data/db_loader.py`: build labels and features from separate time windows.
- `ml_service/models/train_risk.py`, `models/train_regression.py`: grouped/out-of-time splits and honest metrics.
- `ml_service/routers/metrics.py`: expose data provenance.
- `backend/PlateformePFA.API/DTOs/ML/MlMetricsDto.cs`: carry provenance to the UI.
- `database/entrypoint.sh`: run idempotent initialization every boot and publish completion state.
- `database/init.sql`, `database/init_dw.sql`: schema readiness marker, row versions, and fact-grain uniqueness.
- `database/etl.sql`, `backend/PlateformePFA.API/Scripts/etl.sql`: locked, transaction-safe ETL; both copies must remain identical.
- `docker-compose.yml`: readiness checks and ML build-platform behavior.
- `ml_service/Dockerfile`: architecture-aware Microsoft package repository.

## Required task order

Tasks 1–6 secure and stabilize transactional behavior. Tasks 7–9 replace invalid ML training semantics. Tasks 10–12 repair Copilot/frontend workflows. Tasks 13–15 harden deployment and finish with the complete verification gate.

### Task 1: Resolve the merge and restore a trustworthy baseline

**Files:**
- Modify: `frontend/src/pages/Enseignant.tsx`
- Modify: `frontend/src/pages/Predictions.tsx`
- Test: existing frontend and backend test suites

- [ ] **Step 1: Record both conflict stages before editing**

Run:

```powershell
git status --short
git show :2:frontend/src/pages/Enseignant.tsx > $env:TEMP\Enseignant.ours.tsx
git show :3:frontend/src/pages/Enseignant.tsx > $env:TEMP\Enseignant.theirs.tsx
git show :2:frontend/src/pages/Predictions.tsx > $env:TEMP\Predictions.ours.tsx
git show :3:frontend/src/pages/Predictions.tsx > $env:TEMP\Predictions.theirs.tsx
```

Expected: both `:2:` and `:3:` versions are captured and the two files remain `UU` until the merge is intentionally resolved.

- [ ] **Step 2: Merge behavior, not just text**

Keep the Enseignant module-assignment flow, note entry, absence entry, loading/error states, and current role-aware navigation. Keep the Predictions page’s current summary, batch execution, metrics, academic-year filtering, and ML-unavailable rendering. Remove every conflict marker:

```powershell
rg -n "^(<<<<<<<|=======|>>>>>>>)" frontend/src/pages/Enseignant.tsx frontend/src/pages/Predictions.tsx
```

Expected: no matches.

- [ ] **Step 3: Mark the merge resolved and run the complete frontend gate**

```powershell
git add frontend/src/pages/Enseignant.tsx frontend/src/pages/Predictions.tsx
npm run test --prefix frontend
npm run build --prefix frontend
npm run lint --prefix frontend
```

Expected: 8 or more tests pass; build and lint exit 0. Fix the existing `AppShell.tsx` hook dependency warning by including `setCopilotOpen` in the dependency array if lint still reports it.

- [ ] **Step 4: Commit**

```powershell
git add frontend/src/pages/Enseignant.tsx frontend/src/pages/Predictions.tsx frontend/src/layout/AppShell.tsx
git commit -m "fix: resolve frontend integration conflicts"
```

### Task 2: Centralize and enforce academic resource authorization

**Files:**
- Create: `backend/PlateformePFA.API/Services/IAcademicAccessService.cs`
- Create: `backend/PlateformePFA.API/Services/AcademicAccessService.cs`
- Modify: `backend/PlateformePFA.API/Program.cs`
- Modify: `backend/PlateformePFA.API/Controllers/NotesController.cs`
- Modify: `backend/PlateformePFA.API/Controllers/AbsencesController.cs`
- Modify: `backend/PlateformePFA.API/Controllers/RapportsController.cs`
- Modify: `backend/PlateformePFA.API/Services/ReportGenerator.cs`
- Test: `backend/PlateformePFA.Tests/Controllers/NotesControllerTests.cs`
- Create: `backend/PlateformePFA.Tests/Controllers/AbsencesControllerTests.cs`
- Modify: `backend/PlateformePFA.Tests/Controllers/RapportsControllerTests.cs`

- [ ] **Step 1: Write failing cross-module authorization tests**

Add an Enseignant token helper and tests with a teacher assigned to module A:

```csharp
[Fact]
public async Task Enseignant_cannot_create_note_for_another_module()
{
    var client = await CreateTeacherClientAsync(assignedModuleId);
    var response = await client.PostAsJsonAsync("/api/notes", new {
        etudiantId, moduleId = otherModuleId, noteFinal = 12m,
        annee = "2025/2026", semestre = "S1"
    });
    response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
}

[Fact]
public async Task Enseignant_cannot_update_absence_for_another_module()
{
    var client = await CreateTeacherClientAsync(assignedModuleId);
    var response = await client.PutAsJsonAsync($"/api/absences/{otherModuleAbsenceId}", new {
        etudiantId, moduleId = otherModuleId, date = DateTime.UtcNow.Date,
        nombreHeures = 2, justifiee = false
    });
    response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
}
```

Also assert that list endpoints return only the assigned module and that a teacher cannot generate `TOUS` or download another user’s report.

- [ ] **Step 2: Verify the tests fail for the current role-only checks**

```powershell
dotnet test backend/PlateformePFA.Tests/PlateformePFA.Tests.csproj --filter "NotesControllerTests|AbsencesControllerTests|RapportsControllerTests" --nologo
```

Expected: the new tests receive success instead of 403 or contain records from unassigned modules.

- [ ] **Step 3: Add one access policy service**

Use the authenticated `NameIdentifier` claim and assignment stored on `Utilisateur.ModuleId`:

```csharp
public interface IAcademicAccessService
{
    Task<int?> GetAssignedModuleIdAsync(ClaimsPrincipal user, CancellationToken ct = default);
    Task<bool> CanAccessModuleAsync(ClaimsPrincipal user, int moduleId, CancellationToken ct = default);
}

public sealed class AcademicAccessService(AppDbContext db) : IAcademicAccessService
{
    public async Task<int?> GetAssignedModuleIdAsync(ClaimsPrincipal user, CancellationToken ct = default)
    {
        if (!user.IsInRole("Enseignant")) return null;
        var id = int.Parse(user.FindFirstValue(ClaimTypes.NameIdentifier)!);
        return await db.Utilisateurs.Where(u => u.Id == id && u.EstActif)
            .Select(u => u.ModuleId).SingleOrDefaultAsync(ct);
    }

    public async Task<bool> CanAccessModuleAsync(ClaimsPrincipal user, int moduleId, CancellationToken ct = default) =>
        !user.IsInRole("Enseignant") || await GetAssignedModuleIdAsync(user, ct) == moduleId;
}
```

Register it with `AddScoped<IAcademicAccessService, AcademicAccessService>()`.

- [ ] **Step 4: Apply the scope to reads, writes, and reports**

For a teacher mutation, return `Forbid()` before changing tracked entities:

```csharp
if (!await _access.CanAccessModuleAsync(User, dto.ModuleId, ct))
    return Forbid();
```

For list/report queries, derive the module filter from the caller rather than accepting a wider client filter. Report downloads must require Admin/Responsable or `rapport.CreePar == currentUserId`; teachers may generate reports only for their assigned module.

- [ ] **Step 5: Verify and commit**

```powershell
dotnet test backend/PlateformePFA.Tests/PlateformePFA.Tests.csproj --filter "NotesControllerTests|AbsencesControllerTests|RapportsControllerTests" --nologo
git add backend/PlateformePFA.API backend/PlateformePFA.Tests
git commit -m "fix: enforce teacher resource ownership"
```

Expected: teacher cross-module access is 403, scoped reads contain only the assignment, and Admin/Responsable behavior remains green.

### Task 3: Enforce academic-record consistency and optimistic concurrency

**Files:**
- Modify: `backend/PlateformePFA.API/Models/Note.cs`
- Modify: `backend/PlateformePFA.API/Models/Presence.cs`
- Modify: `backend/PlateformePFA.API/DTOs/Notes/NoteDto.cs`
- Modify: `backend/PlateformePFA.API/DTOs/Absences/AbsenceDto.cs`
- Modify: `backend/PlateformePFA.API/Data/AppDbContext.cs`
- Modify: `backend/PlateformePFA.API/Data/RuntimeMigrations.cs`
- Modify: `backend/PlateformePFA.API/Controllers/NotesController.cs`
- Modify: `backend/PlateformePFA.API/Controllers/AbsencesController.cs`
- Modify: `database/init.sql`
- Test: note and absence controller tests

- [ ] **Step 1: Add failing consistency and stale-write tests**

Create tests that submit a GI student with an IA module and expect 400, plus a stale row version and expect 409:

```csharp
response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
(await response.Content.ReadAsStringAsync()).Should().Contain("filière");

staleResponse.StatusCode.Should().Be(HttpStatusCode.Conflict);
```

- [ ] **Step 2: Add row versions to models and DTOs**

```csharp
[Timestamp]
public byte[] RowVersion { get; set; } = Array.Empty<byte>();
```

Expose row versions as Base64 strings in read/update DTOs. Before `SaveChangesAsync`, apply the submitted version:

```csharp
_context.Entry(note).Property(n => n.RowVersion).OriginalValue = Convert.FromBase64String(dto.RowVersion);
```

Return 409 from `DbUpdateConcurrencyException` with `{ message = "La ressource a été modifiée par un autre utilisateur." }`.

- [ ] **Step 3: Validate student/module compatibility once per mutation**

```csharp
var compatible = await _context.Etudiants
    .Where(e => e.Id == dto.EtudiantId && e.DesinscritLe == null)
    .Join(_context.Modules.Where(m => m.Id == dto.ModuleId),
          e => e.FiliereId, m => m.FiliereId, (e, m) => new { e, m })
    .AnyAsync(x => x.e.Niveau == x.m.Niveau, ct);
if (!compatible)
    return BadRequest(new { message = "L'étudiant et le module doivent appartenir à la même filière et au même niveau." });
```

- [ ] **Step 4: Add idempotent SQL schema changes**

Add `ROWVERSION NOT NULL` columns to both initial schema and runtime migration. Do not attempt to insert or update row-version values manually.

- [ ] **Step 5: Verify and commit**

```powershell
dotnet test backend/PlateformePFA.Tests/PlateformePFA.Tests.csproj --filter "NotesControllerTests|AbsencesControllerTests" --nologo
git add backend/PlateformePFA.API backend/PlateformePFA.Tests database/init.sql
git commit -m "fix: protect academic record integrity"
```

### Task 4: Make alert lifecycle updates symmetric

**Files:**
- Modify: `backend/PlateformePFA.API/Services/AlerteService.cs`
- Modify: `backend/PlateformePFA.API/Controllers/AbsencesController.cs`
- Modify: `backend/PlateformePFA.Tests/Services/AlerteServiceTests.cs`
- Modify: `backend/PlateformePFA.Tests/Controllers/AbsencesControllerTests.cs`

- [ ] **Step 1: Write failing correction and deletion tests**

Seed an active `AbsenceExcessive` alert, reduce unjustified hours to 20, and assert `Resolue == true`. Add a second test that deletes enough absences to fall below the threshold and expects the same result.

- [ ] **Step 2: Verify RED**

```powershell
dotnet test backend/PlateformePFA.Tests/PlateformePFA.Tests.csproj --filter "AlerteServiceTests|AbsencesControllerTests" --nologo
```

Expected: the alert remains unresolved.

- [ ] **Step 3: Resolve or create from the same evaluator**

```csharp
var existing = await _context.Alertes.SingleOrDefaultAsync(a =>
    a.EtudiantId == etudiantId && a.Type == "AbsenceExcessive" && !a.Resolue, ct);

if (heuresInjustifiees <= 20)
{
    if (existing is not null) {
        existing.Resolue = true;
        existing.ResolueeLe = DateTime.UtcNow;
    }
    await _context.SaveChangesAsync(ct);
    return;
}
```

Call this evaluator after create, update, justification changes, and deletion.

- [ ] **Step 4: Verify and commit**

```powershell
dotnet test backend/PlateformePFA.Tests/PlateformePFA.Tests.csproj --filter "AlerteServiceTests|AbsencesControllerTests" --nologo
git add backend/PlateformePFA.API/Services/AlerteService.cs backend/PlateformePFA.API/Controllers/AbsencesController.cs backend/PlateformePFA.Tests
git commit -m "fix: reconcile absence alerts after mutations"
```

### Task 5: Make refresh-token rotation single-use under concurrency

**Files:**
- Modify: `backend/PlateformePFA.API/Services/AuthService.cs`
- Modify: `backend/PlateformePFA.Tests/Controllers/AuthControllerTests.cs`
- Modify: `backend/PlateformePFA.Tests/PlateformePFA.Tests.csproj`

- [ ] **Step 1: Add a SQL Server-backed concurrency integration test**

The existing in-memory provider cannot reproduce conditional-update concurrency. Add a test fixture using a disposable SQL Server test database, log in once, issue two simultaneous refreshes, and assert exactly one 200 and one 401:

```csharp
var responses = await Task.WhenAll(
    client.PostAsJsonAsync("/api/auth/refresh", new { refreshToken }),
    client.PostAsJsonAsync("/api/auth/refresh", new { refreshToken }));
responses.Count(r => r.StatusCode == HttpStatusCode.OK).Should().Be(1);
responses.Count(r => r.StatusCode == HttpStatusCode.Unauthorized).Should().Be(1);
```

- [ ] **Step 2: Consume the token with one conditional update**

```csharp
var consumed = await _context.RefreshTokens
    .Where(rt => rt.Id == storedToken.Id && rt.RevokedAt == null && rt.ExpiresAt > DateTime.UtcNow)
    .ExecuteUpdateAsync(setters => setters
        .SetProperty(rt => rt.RevokedAt, DateTime.UtcNow), ct);
if (consumed != 1) return null;
```

Then create the replacement token. Wrap consumption and creation in an explicit transaction so creation failure rolls back the consumption.

- [ ] **Step 3: Verify sequential and concurrent behavior**

```powershell
dotnet test backend/PlateformePFA.Tests/PlateformePFA.Tests.csproj --filter AuthControllerTests --nologo
```

Expected: existing login/refresh tests pass; concurrent replay yields one replacement only.

- [ ] **Step 4: Commit**

```powershell
git add backend/PlateformePFA.API/Services/AuthService.cs backend/PlateformePFA.Tests
git commit -m "fix: rotate refresh tokens atomically"
```

### Task 6: Preserve prediction failure states and enforce the ML contract

**Files:**
- Modify: `backend/PlateformePFA.API/Controllers/PredictionsController.cs`
- Modify: `backend/PlateformePFA.API/Services/RiskScorer.cs`
- Modify: `backend/PlateformePFA.API/Models/PredictionML.cs`
- Modify: `backend/PlateformePFA.Tests/Controllers/PredictionsControllerTests.cs`
- Modify: `ml_service/schemas/prediction_schema.py`

- [ ] **Step 1: Add failing outage, malformed-response, and period tests**

Use a fake `MLService` handler for: HTTP 503, HTTP 200 with `{}`, HTTP 200 with probability `1.5`, and a valid response. Assert failures have `Status = "MlUnavailable"` or `"InvalidResponse"`, `ScoreRisque == null`, and do not replace the last successful score. Seed notes from two academic years and assert only the configured year contributes features.

- [ ] **Step 2: Verify RED**

```powershell
dotnet test backend/PlateformePFA.Tests/PlateformePFA.Tests.csproj --filter PredictionsControllerTests --nologo
```

- [ ] **Step 3: Parse responses through one strict contract**

```csharp
private sealed record MlPredictionResponse(decimal probabilite, string niveau_risque);

var payload = await response.Content.ReadFromJsonAsync<MlPredictionResponse>(cancellationToken: ct);
if (!response.IsSuccessStatusCode || payload is null || payload.probabilite is < 0 or > 1 ||
    payload.niveau_risque is not ("Faible" or "Moyen" or "Élevé"))
{
    return PredictionAttempt.Failed(response.IsSuccessStatusCode ? "InvalidResponse" : "MlUnavailable");
}
```

Persist a failure audit row only if the model supports a nullable score; never synthesize `0`/`Faible`. Change `RiskScorer` to require `p.Status == "Ok" && p.ScoreRisque != null`.

- [ ] **Step 4: Scope and validate features**

Filter notes by `Annee == selectedYear` and modules by the selected semester when present. Filter absences using the semester date window from `ReportGenerator.SemesterDateRange` extracted into a shared `AcademicPeriod` helper. Align `nb_modules` validation between backend and Pydantic; reject invalid input before calling ML rather than capping silently.

- [ ] **Step 5: Verify and commit**

```powershell
dotnet test backend/PlateformePFA.Tests/PlateformePFA.Tests.csproj --filter PredictionsControllerTests --nologo
python -m pytest ml_service/tests -q
git add backend/PlateformePFA.API backend/PlateformePFA.Tests ml_service/schemas/prediction_schema.py
git commit -m "fix: preserve ML prediction failure semantics"
```

### Task 7: Replace the fabricated risk label with a future observed outcome

**Files:**
- Modify: `ml_service/data/db_loader.py`
- Modify: `ml_service/models/train_risk.py`
- Create: `ml_service/tests/test_risk_training_data.py`
- Create: `ml_service/tests/test_risk_training.py`

- [ ] **Step 1: Define the temporal training contract in tests**

For each `(EtudiantId, Annee, Semestre)`, features come from the completed prior period and the label is failure in the following period. Write a fixture where changing a future grade changes only `at_risk`, never the feature columns:

```python
features, labels, groups, periods = build_risk_dataset(notes, absences)
assert list(features.columns) == ["moyenne_generale", "taux_absence", "nb_modules"]
assert labels.loc[target_row] == 1
assert features.loc[target_row, "moyenne_generale"] == 11.5
assert groups.loc[target_row] == student_id
```

- [ ] **Step 2: Verify RED**

```powershell
python -m pytest ml_service/tests/test_risk_training_data.py -q
```

Expected: the loader currently has no temporal dataset builder and derives labels from features.

- [ ] **Step 3: Build prior-period features and future labels**

Aggregate each student-period once, sort by academic period, shift the observed next-period outcome backward, and drop the last period because it has no known future label:

```python
periods["future_failed"] = periods.groupby("EtudiantId")["failed"].shift(-1)
dataset = periods.dropna(subset=["future_failed"]).copy()
y = dataset.pop("future_failed").astype("int8")
groups = dataset.pop("EtudiantId")
period_key = dataset.pop("period_key")
X = dataset[["moyenne_generale", "taux_absence", "nb_modules"]]
```

Do not inject random label flips.

- [ ] **Step 4: Use grouped, out-of-time evaluation**

Hold out the latest academic period. If only one period exists, use `GroupShuffleSplit` by student and mark the evaluation strategy in metadata. Assert no student appears in both train and test.

- [ ] **Step 5: Verify and commit**

```powershell
python -m pytest ml_service/tests/test_risk_training_data.py ml_service/tests/test_risk_training.py -q
git add ml_service/data/db_loader.py ml_service/models/train_risk.py ml_service/tests
git commit -m "fix: train risk model on future outcomes"
```

### Task 8: Remove forecast target leakage and split by student/time

**Files:**
- Modify: `ml_service/data/db_loader.py`
- Modify: `ml_service/models/train_regression.py`
- Create: `ml_service/tests/test_forecast_training_data.py`
- Create: `ml_service/tests/test_forecast_training.py`

- [ ] **Step 1: Add a leakage regression test**

Construct a student with S1 and S2 grades. Assert the S2 target is predicted only from S1 aggregates and changing S2 does not change `moyenne_actuelle`:

```python
first = build_forecast_dataset(rows)
rows.loc[rows.Semestre == "S2", "NoteFinale"] = 20
second = build_forecast_dataset(rows)
assert first.X.loc[s2_key, "moyenne_actuelle"] == second.X.loc[s2_key, "moyenne_actuelle"]
assert first.y.loc[s2_key] != second.y.loc[s2_key]
```

- [ ] **Step 2: Implement lagged feature construction**

Create one row per student-period, calculate features from periods strictly before the target, and use the next period’s average as `y`. Drop targets without history.

- [ ] **Step 3: Replace random row splitting**

Use the same latest-period holdout/grouped fallback as risk training. Persist `split_strategy`, train/test periods, unique student counts, MAE, and R² in metadata.

- [ ] **Step 4: Verify and commit**

```powershell
python -m pytest ml_service/tests/test_forecast_training_data.py ml_service/tests/test_forecast_training.py -q
git add ml_service/data/db_loader.py ml_service/models/train_regression.py ml_service/tests
git commit -m "fix: remove forecast target leakage"
```

### Task 9: Surface model provenance and evaluation strategy

**Files:**
- Modify: `ml_service/routers/metrics.py`
- Modify: `ml_service/tests/test_metrics.py`
- Modify: `backend/PlateformePFA.API/DTOs/ML/MlMetricsDto.cs`
- Modify: `backend/PlateformePFA.API/Controllers/PredictionsController.cs`
- Modify: `frontend/src/pages/Predictions.tsx`
- Modify: `frontend/package.json`
- Modify: `frontend/package-lock.json`
- Modify: `frontend/vite.config.ts`
- Create: `frontend/src/test/setup.ts`
- Create: `frontend/src/pages/Predictions.test.tsx`

- [ ] **Step 1: Add the frontend component-test harness**

```powershell
npm install --prefix frontend --save-dev @testing-library/react @testing-library/jest-dom jsdom
```

Set Vitest's environment to `jsdom`, add `setupFiles: ['./src/test/setup.ts']`, and load matchers from the setup file:

```ts
import '@testing-library/jest-dom/vitest'
```

- [ ] **Step 2: Extend the metrics contract tests**

Require these fields for each trained model:

```python
assert body["risk"]["data_source"] in {"dw", "synthetic"}
assert body["risk"]["split_strategy"] in {"out_of_time", "grouped_student"}
```

- [ ] **Step 3: Return and proxy provenance**

Add `data_source`, `split_strategy`, `trained_at`, and evaluated periods to the FastAPI response and matching nullable C# DTO properties. Preserve unknown fields for backward compatibility.

- [ ] **Step 4: Render a non-dismissible synthetic-data warning**

When any model reports `data_source === 'synthetic'`, render: “Modèle de démonstration entraîné sur des données synthétiques — ne pas utiliser pour une décision pédagogique.” Do not label synthetic metrics as production validation.

- [ ] **Step 5: Verify and commit**

```powershell
python -m pytest ml_service/tests/test_metrics.py -q
npm run test --prefix frontend -- Predictions.test.tsx
npm run build --prefix frontend
git add ml_service backend/PlateformePFA.API/DTOs/ML backend/PlateformePFA.API/Controllers/PredictionsController.cs frontend/src/pages/Predictions.tsx frontend/src/pages/Predictions.test.tsx
git commit -m "feat: expose ML training provenance"
```

### Task 10: Authenticate Copilot inference and preserve identity across streams

**Files:**
- Create: `backend/PlateformePFA.API/Controllers/CopilotSessionController.cs`
- Create: `backend/PlateformePFA.Tests/Controllers/CopilotSessionControllerTests.cs`
- Create: `copilot-runtime/src/auth.ts`
- Create: `copilot-runtime/src/auth.test.ts`
- Modify: `copilot-runtime/src/index.ts`
- Modify: `copilot-runtime/src/tools.ts`
- Modify: `copilot-runtime/package.json`
- Modify: `copilot-runtime/package-lock.json`
- Modify: `frontend/src/context/AuthContext.tsx`

- [ ] **Step 1: Add the runtime test command and failing session tests**

Install Vitest and add `"test": "vitest run"` to `copilot-runtime/package.json`:

```powershell
npm install --prefix copilot-runtime --save-dev vitest
```

Backend tests require authentication for `POST /api/copilot/session`, assert a cookie named `pfa_copilot_session` with `HttpOnly`, `SameSite=Strict`, and path `/api/copilotkit`, and assert logout expires it. Runtime tests assert missing/invalid cookies return 401 and a valid cookie reaches a tool with the same JWT.

- [ ] **Step 2: Issue a short-lived Copilot JWT cookie**

Create a JWT with the current user ID, role, issuer, and audience, expiring after 15 minutes. Set it through:

```csharp
Response.Cookies.Append("pfa_copilot_session", token, new CookieOptions {
    HttpOnly = true,
    Secure = !env.IsDevelopment(),
    SameSite = SameSiteMode.Strict,
    Path = "/api/copilotkit",
    MaxAge = TimeSpan.FromMinutes(15)
});
```

Provide authenticated `POST /api/copilot/session` and `DELETE /api/copilot/session` endpoints.

- [ ] **Step 3: Require the cookie in the runtime**

Parse `pfa_copilot_session`, verify HS256 plus issuer/audience, and reject every inference request lacking it. Keep only `GET /health` anonymous. Run the handler inside `authStore.run(verifiedToken, ...)`, ensuring tools forward the verified JWT.

- [ ] **Step 4: Establish and clear sessions from React**

After successful login call `api.post('/copilot/session', {}, { withCredentials: true })`; on logout call `api.delete('/copilot/session', { withCredentials: true })`. If session creation fails, keep the core application logged in but disable Copilot with an explicit unavailable message.

- [ ] **Step 5: Verify and commit**

```powershell
dotnet test backend/PlateformePFA.Tests/PlateformePFA.Tests.csproj --filter CopilotSessionControllerTests --nologo
npm test --prefix copilot-runtime
npm exec --prefix copilot-runtime -- tsc --noEmit
git add backend/PlateformePFA.API backend/PlateformePFA.Tests copilot-runtime frontend/src/context/AuthContext.tsx
git commit -m "fix: authenticate Copilot streaming sessions"
```

### Task 11: Unify alert draft, confirmation, and cancellation

**Files:**
- Modify: `backend/PlateformePFA.API/Controllers/CopilotToolController.cs`
- Modify: `backend/PlateformePFA.Tests/Controllers/CopilotToolControllerTests.cs`
- Modify: `copilot-runtime/src/tools.ts`
- Modify: `frontend/src/pages/Students.tsx`
- Modify: `frontend/src/components/copilot/ConfirmCard.tsx`
- Create: `frontend/src/components/copilot/ConfirmCard.test.tsx`

- [ ] **Step 1: Add failing lifecycle tests**

Test `draft_alert` returns a real positive ID; only its creator, Admin, or Responsable can confirm/cancel it; confirmation creates exactly one alert; repeated confirmation is idempotent; expired/cancelled drafts cannot send.

- [ ] **Step 2: Add explicit lifecycle endpoints**

Implement:

```text
POST /api/copilot/drafts/{id}/confirm
POST /api/copilot/drafts/{id}/cancel
```

Perform the status transition and alert creation in one transaction using a conditional update from `pending_user_confirm`. Return 409 for expired or already-finalized drafts.

- [ ] **Step 3: Keep one tool schema**

The canonical tool arguments are:

```ts
z.object({ etudiant_id: z.number().int().positive(), message_fr: z.string().min(1), severity: z.enum(['info', 'warning', 'critical']) })
```

Remove the duplicate frontend `useCopilotAction({ name: 'draft_alert' })`. The UI must render the server result `{ draftId, studentId, message, severity, expiresAt }` and call the lifecycle endpoints.

- [ ] **Step 4: Verify and commit**

```powershell
dotnet test backend/PlateformePFA.Tests/PlateformePFA.Tests.csproj --filter CopilotToolControllerTests --nologo
npm run test --prefix frontend -- ConfirmCard.test.tsx
git add backend/PlateformePFA.API/Controllers/CopilotToolController.cs backend/PlateformePFA.Tests/Controllers/CopilotToolControllerTests.cs copilot-runtime/src/tools.ts frontend/src
git commit -m "fix: unify Copilot alert confirmation"
```

### Task 12: Repair frontend contracts, role actions, account isolation, and alert caches

**Files:**
- Modify: `frontend/src/layout/CommandPalette.tsx`
- Modify: `frontend/src/layout/Topbar.tsx`
- Modify: `frontend/src/context/AuthContext.tsx`
- Modify: `frontend/src/pages/Alerts.tsx`
- Create: `frontend/src/layout/CommandPalette.test.tsx`
- Create: `frontend/src/context/AuthContext.test.tsx`
- Create: `frontend/src/pages/Alerts.test.tsx`

- [ ] **Step 1: Add failing frontend regression tests**

Mock `/etudiants/with-stats` as an array and assert risk suggestions render. Log in as user A, seed `['enseignant-module']`, log in as user B, and assert the old cache is absent. Resolve alerts singly and in batch and assert invalidation of `['alertes']`, `['topbar','alertes-unresolved']`, and `['sidebar','alertes-unresolved']`. Assert Enseignant cannot see “Ajouter un étudiant” or “Prédictions batch”.

- [ ] **Step 2: Consume the list response correctly**

```ts
const { data = [] } = useQuery<EtudiantRow[]>({
  queryKey: ['command-palette', 'students'],
  queryFn: async () => (await api.get<EtudiantRow[]>('/etudiants/with-stats')).data,
})
const risky = data.filter((student) => student.niveauRisque === 'Élevé')
```

- [ ] **Step 3: Isolate caches between identities**

Call `await queryClient.cancelQueries(); queryClient.clear()` immediately before installing the new token/user during login. Redirect authenticated users away from `/login` as a second guard.

- [ ] **Step 4: Centralize frontend permissions and invalidations**

Extend `frontend/src/auth/roles.ts` with pure `canCreateStudent(role)` and `canRunBatchPredictions(role)` helpers and use them in Topbar and CommandPalette. After either alert resolution mutation:

```ts
await Promise.all([
  queryClient.invalidateQueries({ queryKey: ['alertes'] }),
  queryClient.invalidateQueries({ queryKey: ['topbar', 'alertes-unresolved'] }),
  queryClient.invalidateQueries({ queryKey: ['sidebar', 'alertes-unresolved'] }),
])
```

- [ ] **Step 5: Verify and commit**

```powershell
npm run test --prefix frontend
npm run build --prefix frontend
npm run lint --prefix frontend
git add frontend/src
git commit -m "fix: align frontend state and authorization contracts"
```

### Task 13: Make database initialization and readiness truthful

**Files:**
- Modify: `database/entrypoint.sh`
- Modify: `database/init.sql`
- Modify: `database/init_dw.sql`
- Modify: `docker-compose.yml`
- Modify: `backend/PlateformePFA.API/Program.cs`
- Create: `backend/PlateformePFA.API/Health/DatabaseReadyHealthCheck.cs`
- Create: `backend/PlateformePFA.Tests/Health/DatabaseReadyHealthCheckTests.cs`

- [ ] **Step 1: Add a schema-completion marker**

Create `dbo.SchemaState` idempotently in `PFA_DB`, with one row `Component='core', Version=<integer>`. Update the row only after all OLTP, DW, login, and grant scripts succeed.

- [ ] **Step 2: Run idempotent scripts on every boot**

Remove the `PFA_DB exists -> skip` branch. Always run `init.sql`, `init_dw.sql`, and `copilot_readonly.sql` with `-b`; all scripts must remain idempotent. Exit nonzero on failure before waiting on SQL Server.

- [ ] **Step 3: Gate Compose on schema readiness**

Change the DB healthcheck query to require the expected marker:

```yaml
test: ["CMD-SHELL", "SQLCMDPASSWORD=$${SA_PASSWORD} /opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -d PFA_DB -C -b -Q \"IF NOT EXISTS (SELECT 1 FROM dbo.SchemaState WHERE Component='core' AND Version >= 1) THROW 51000, 'schema not ready', 1\" "]
```

Use `$${...}` so expansion happens inside the container rather than leaking the value into generated Compose configuration.

- [ ] **Step 4: Fail startup on migration/seeding errors and add readiness**

Do not catch-and-continue around `RuntimeMigrations.Apply` or `DataSeeder.Initialize`. Keep `/health` as liveness and add `/ready` backed by `Database.CanConnectAsync()` plus the schema marker. Point the backend Docker healthcheck and nginx startup gate at `/ready`.

- [ ] **Step 5: Verify cold, existing, and partial volumes**

```powershell
docker compose config --quiet
docker compose down -v
docker compose up -d --build db
docker compose up -d backend
docker compose restart db
docker compose ps
```

Expected: cold boot becomes healthy only after initialization; restart reruns scripts safely; deleting the marker makes DB/backend readiness fail instead of returning healthy.

- [ ] **Step 6: Commit**

```powershell
git add database docker-compose.yml backend/PlateformePFA.API backend/PlateformePFA.Tests
git commit -m "fix: make database readiness reflect schema state"
```

### Task 14: Make ETL atomic, serialized, and unique at fact grain

**Files:**
- Modify: `database/init_dw.sql`
- Modify: `database/etl.sql`
- Modify: `backend/PlateformePFA.API/Scripts/etl.sql`
- Modify: `backend/PlateformePFA.API/Controllers/AdminController.cs`
- Create: `backend/PlateformePFA.Tests/Controllers/AdminControllerSqlTests.cs`

- [ ] **Step 1: Add a SQL integration test**

Run two `/api/admin/sync-dw` calls concurrently against SQL Server. Assert one succeeds while the other returns 409 or waits and succeeds, and assert no duplicate `(EtudiantKey, ModuleKey, TempsKey)` rows. Add a forced-failure test after dimension synchronization and assert all counts roll back.

- [ ] **Step 2: Add the unique fact-grain constraint**

Before creating the index, detect duplicates and fail with a diagnostic rather than silently deleting data:

```sql
IF EXISTS (SELECT 1 FROM FaitNotes GROUP BY EtudiantKey, ModuleKey, TempsKey HAVING COUNT(*) > 1)
    THROW 51001, 'Duplicate FaitNotes grain must be repaired before migration', 1;
CREATE UNIQUE INDEX UX_FaitNotes_Grain ON FaitNotes(EtudiantKey, ModuleKey, TempsKey);
```

Wrap index creation in the standard `IF NOT EXISTS` check.

- [ ] **Step 3: Serialize and transact ETL**

At the start of the canonical script:

```sql
SET XACT_ABORT ON;
BEGIN TRANSACTION;
DECLARE @lock_result int;
EXEC @lock_result = sys.sp_getapplock @Resource='PFA_DW_ETL', @LockMode='Exclusive', @LockOwner='Transaction', @LockTimeout=0;
IF @lock_result < 0 THROW 51002, 'ETL already running', 1;
```

Commit only after all dimensions and facts succeed. Let `XACT_ABORT` roll back on errors. Copy `database/etl.sql` byte-for-byte to the backend script.

- [ ] **Step 4: Execute the script as one transaction-aware unit**

Keep `GO` batch parsing if required, but reuse the same open `SqlConnection` and `SqlTransaction`, or remove `GO` from the script so the complete script executes once. Map lock error 51002 to HTTP 409.

- [ ] **Step 5: Verify and commit**

```powershell
Compare-Object (Get-Content database/etl.sql) (Get-Content backend/PlateformePFA.API/Scripts/etl.sql)
dotnet test backend/PlateformePFA.Tests/PlateformePFA.Tests.csproj --filter AdminControllerSqlTests --nologo
git add database/init_dw.sql database/etl.sql backend/PlateformePFA.API/Scripts/etl.sql backend/PlateformePFA.API/Controllers/AdminController.cs backend/PlateformePFA.Tests/Controllers/AdminControllerSqlTests.cs
git commit -m "fix: serialize and atomically commit warehouse ETL"
```

Expected: `Compare-Object` emits nothing; concurrency and rollback tests pass.

### Task 15: Make the ML image portable and run the final release gate

**Files:**
- Modify: `ml_service/Dockerfile`
- Modify: `docker-compose.yml`
- Modify: `README.md`
- Verify: entire repository

- [ ] **Step 1: Make the Microsoft repository architecture-aware**

Replace hard-coded `arch=amd64` with:

```dockerfile
RUN arch="$(dpkg --print-architecture)" \
 && curl -fsSL https://packages.microsoft.com/keys/microsoft.asc | gpg --dearmor -o /usr/share/keyrings/microsoft-prod.gpg \
 && echo "deb [arch=${arch} signed-by=/usr/share/keyrings/microsoft-prod.gpg] https://packages.microsoft.com/debian/12/prod bookworm main" > /etc/apt/sources.list.d/mssql-release.list \
 && apt-get update \
 && ACCEPT_EULA=Y apt-get install -y --no-install-recommends msodbcsql18 unixodbc \
 && rm -rf /var/lib/apt/lists/*
```

If Microsoft does not publish `msodbcsql18` for a supported target, document and enforce `platform: linux/amd64` in Compose instead of allowing a late opaque package failure.

- [ ] **Step 2: Document operational changes**

Update README sections for Copilot session cookies, liveness versus readiness, idempotent DB initialization, model provenance, retraining prerequisites, ETL conflict response, and supported container architectures.

- [ ] **Step 3: Run the complete verification gate**

```powershell
dotnet test backend/PlateformePFA.Tests/PlateformePFA.Tests.csproj --configuration Release --nologo
npm ci --prefix frontend
npm run test --prefix frontend
npm run build --prefix frontend
npm run lint --prefix frontend
npm ci --prefix copilot-runtime
npm test --prefix copilot-runtime
npm exec --prefix copilot-runtime -- tsc --noEmit
python -m pytest ml_service/tests -q
docker compose config --quiet
docker compose build backend frontend ml-service copilot-runtime
git diff --check
rg -n "^(<<<<<<<|=======|>>>>>>>)" --glob '!docs/**' .
```

Expected: all commands exit 0; conflict-marker search has no matches; frontend and runtime production builds succeed; all backend, frontend, Copilot, and ML tests pass.

- [ ] **Step 4: Run one end-to-end smoke path**

Start the stack and verify: Admin login; teacher sees only their module; cross-module grade/absence returns 403; prediction outage does not change last-known risk; synthetic model warning is visible when applicable; anonymous Copilot request returns 401; authenticated Copilot lists a student; alert draft confirms once; ETL sync completes; `/health` and `/ready` return 200 only in their intended states.

- [ ] **Step 5: Commit**

```powershell
git add ml_service/Dockerfile docker-compose.yml README.md
git commit -m "chore: complete review remediation gate"
```

## Coverage matrix

| Review finding | Plan task |
|---|---:|
| Unresolved frontend merge/build failure | 1 |
| Teacher cross-module writes | 2 |
| Teacher institution-wide reads and exports | 2 |
| Student/module filière-level mismatch | 3 |
| Ineffective note/absence concurrency handling | 3 |
| Stale absence alerts after correction/deletion | 4 |
| Concurrent refresh-token replay | 5 |
| ML outage persisted as low risk | 6 |
| Malformed successful ML response accepted | 6 |
| Prediction features mix academic periods | 6 |
| Risk label derived from inputs | 7 |
| Forecast target leakage and row-random split | 8 |
| Synthetic model provenance hidden | 9 |
| Anonymous Copilot inference | 10 |
| Copilot tools lose the user JWT | 10 |
| Duplicate/incompatible alert-draft workflows | 11 |
| Command palette array contract mismatch | 12 |
| Re-login leaks previous QueryClient data | 12 |
| Teacher-visible unauthorized global actions | 12 |
| Alert badges stale after resolution | 12 |
| DB reports healthy before/after failed initialization | 13 |
| Existing/partial DB volume skips initialization | 13 |
| ETL facts can duplicate under concurrent runs | 14 |
| ETL partially commits on later failure | 14 |
| ML Docker image hard-coded to amd64 | 15 |

## Completion criteria

- Every row in the coverage matrix has a passing regression or integration test.
- Authorization is enforced server-side; frontend hiding is only an ergonomic complement.
- Failed or malformed predictions never become valid low-risk scores.
- ML metrics identify data source and use temporally honest, student-isolated evaluation.
- Copilot inference and tools require one verified user identity across the stream.
- Database readiness reflects complete schema/provisioning state.
- ETL is all-or-nothing and unique at its declared fact grain.
- All commands in Task 15 pass from a clean checkout with documented environment variables.
