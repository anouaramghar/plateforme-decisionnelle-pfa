# Intervention R1 Gap-Fixes Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Close the four gaps a review found between the shipped Release-1 intervention feature and the R1 plan's acceptance criteria: auto-assign on case creation, monitoring-period close guard, automatic escalation of critical-overdue cases, and Enseignant scoped contribution access.

**Architecture:** All four are additive backend changes to the existing ASP.NET Core 8 API. Business rules go in the existing pure `CaseWorkflow` state-machine (unit-tested, no I/O); the escalation sweep is a small scoped service driven by a `BackgroundService`; access scoping reuses the existing `IAcademicAccessService`. No new dependencies, no frontend changes required for acceptance.

**Tech Stack:** ASP.NET Core 8 · EF Core (SQL Server in prod, EF InMemory in tests) · xUnit + FluentAssertions · `Microsoft.AspNetCore.Mvc.Testing`.

## Global Constraints

- All new SQL-touching logic must work under **both** SQL Server (prod) and **EF Core InMemory** (tests). No raw SQL in these tasks.
- Roles are the exact strings `"Admin"`, `"Responsable"`, `"Enseignant"`. Case states/priorities are the exact strings already in `CaseWorkflowState` and the `CK_InterventionCases_*` constraints.
- Audit calls use the existing signature: `_audit.LogAsync(action, entityType, entityId, message, userId, userName)`.
- System-originated events use `UtilisateurNom = "Système"` and a null user id, matching existing timeline rows.
- Keep it minimal (ponytail): no new abstraction layers, no config for values that never change. Mark deliberate shortcuts with a `// ponytail:` comment naming the ceiling.

## Pre-flight decision (human, not code — do NOT implement)

The R1 plan explicitly defers Copilot to Future Work, but the working tree contains uncommitted Copilot work (`CopilotToolController`, `copilot-runtime`, `InterventionCopilotTools.tsx`, `AgentSessions`/`AgentAuditLogs`/`AlertDrafts` tables). This plan does **not** touch any of that. Before merging, the team must decide whether to update the plan to admit Copilot into scope or to drop the Copilot work. No task here depends on that decision.

## File map

- Modify: `backend/PlateformePFA.API/Services/CaseWorkflow.cs` — add `MonitoringComplete` fact + close guard (Task 2); add `IsCriticalOverdue` pure rule (Task 3).
- Modify: `backend/PlateformePFA.API/Controllers/InterventionCasesController.cs` — auto-assign owner + comment fix (Task 1); pass `MonitoringComplete` (Task 2); per-action authorization + cohort scoping + private-note hiding (Task 4).
- Create: `backend/PlateformePFA.API/Services/CaseEscalationService.cs` — sweep service (Task 3).
- Create: `backend/PlateformePFA.API/Services/CaseEscalationWorker.cs` — `BackgroundService` driver (Task 3).
- Modify: `backend/PlateformePFA.API/Program.cs` — register escalation service + worker (Task 3).
- Modify: `backend/PlateformePFA.Tests/Services/CaseWorkflowTests.cs` — new unit tests (Tasks 2, 3).
- Create: `backend/PlateformePFA.Tests/Services/CaseEscalationServiceTests.cs` — sweep integration test (Task 3).
- Modify: `backend/PlateformePFA.Tests/Controllers/InterventionCasesControllerTests.cs` — auto-assign + teacher-access tests (Tasks 1, 4).

Run all backend tests with: `dotnet test backend/PlateformePFA.Tests/PlateformePFA.Tests.csproj`

---

### Task 1: Auto-assign owner on case creation

The plan requires new cases to be assigned automatically ("to the student's filière Responsable; assign Admin when no Responsable exists"). `Utilisateur` has no filière link, so this collapses to "a Responsable, else an Admin" — record that ceiling in a comment.

**Files:**
- Modify: `backend/PlateformePFA.API/Controllers/InterventionCasesController.cs` (in `CreateCase`, ~line 90-103; comment at ~line 138-142)
- Test: `backend/PlateformePFA.Tests/Controllers/InterventionCasesControllerTests.cs`

**Interfaces:**
- Consumes: existing `_context.Utilisateurs` (`Utilisateur.Role`, `Utilisateur.EstActif`, `Utilisateur.Id`).
- Produces: `CreateCase` sets `InterventionCase.OwnerId` from `dto.OwnerId ?? ResolveDefaultOwnerAsync()`.

- [ ] **Step 1: Write the failing test**

Add to `InterventionCasesControllerTests.cs` (inside the class):

```csharp
[Fact]
public async Task CreateCase_with_no_owner_falls_back_to_admin()
{
    var client = await AuthedClientAsync();
    int etuId;
    using (var ctx = _factory.CreateContext()) etuId = StudentId(ctx);

    var res = await client.PostAsJsonAsync("/api/intervention-cases", new
    {
        etudiantId = etuId, motif = "No owner given", priorite = "Medium",
    });
    res.StatusCode.Should().Be(HttpStatusCode.Created);

    using var check = _factory.CreateContext();
    var created = check.InterventionCases.OrderByDescending(c => c.Id).First();
    var admin = check.Utilisateurs.Single(u => u.Email == "test-admin@eniad.ma");
    created.OwnerId.Should().Be(admin.Id);
}

[Fact]
public async Task CreateCase_prefers_a_responsable_over_admin()
{
    using (var seed = _factory.CreateContext())
    {
        if (!seed.Utilisateurs.Any(u => u.Email == "resp1@eniad.ma"))
        {
            seed.Utilisateurs.Add(new Utilisateur
            {
                Email = "resp1@eniad.ma", Nom = "Resp", Prenom = "One",
                MotDePasseHash = "x", Role = "Responsable", EstActif = true,
            });
            seed.SaveChanges();
        }
    }
    var client = await AuthedClientAsync();
    int etuId;
    using (var ctx = _factory.CreateContext()) etuId = StudentId(ctx);

    var res = await client.PostAsJsonAsync("/api/intervention-cases", new
    {
        etudiantId = etuId, motif = "Prefer responsable", priorite = "Medium",
    });
    res.StatusCode.Should().Be(HttpStatusCode.Created);

    using var check = _factory.CreateContext();
    var created = check.InterventionCases.OrderByDescending(c => c.Id).First();
    var resp = check.Utilisateurs.Single(u => u.Email == "resp1@eniad.ma");
    created.OwnerId.Should().Be(resp.Id);
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test backend/PlateformePFA.Tests/PlateformePFA.Tests.csproj --filter "CreateCase_with_no_owner_falls_back_to_admin|CreateCase_prefers_a_responsable_over_admin"`
Expected: FAIL — `created.OwnerId` is `null` (no auto-assign yet).

- [ ] **Step 3: Add the resolver and wire it into CreateCase**

In `InterventionCasesController.cs`, add this private method (place it next to `CurrentUser`):

```csharp
// Default owner when the caller doesn't pick one. ponytail: Utilisateur has no
// filière link, so "the filière Responsable" collapses to "a Responsable, else
// an Admin". Add filière scoping when a User→Filière mapping and a second
// Responsable exist.
private async Task<int?> ResolveDefaultOwnerAsync()
{
    var responsable = await _context.Utilisateurs.AsNoTracking()
        .Where(u => u.EstActif && u.Role == "Responsable")
        .OrderBy(u => u.Id).Select(u => (int?)u.Id).FirstOrDefaultAsync();
    if (responsable != null) return responsable;
    return await _context.Utilisateurs.AsNoTracking()
        .Where(u => u.EstActif && u.Role == "Admin")
        .OrderBy(u => u.Id).Select(u => (int?)u.Id).FirstOrDefaultAsync();
}
```

In `CreateCase`, change the `OwnerId` assignment. Replace:

```csharp
                OwnerId    = dto.OwnerId,
```

with:

```csharp
                OwnerId    = dto.OwnerId ?? await ResolveDefaultOwnerAsync(),
```

- [ ] **Step 4: Fix the misleading link-signals comment**

In `InterventionCasesController.cs`, find the comment block above `LinkSignals` and replace the line:

```csharp
        // re-linking an already-linked signal is rejected, not silently ignored.
```

with:

```csharp
        // validated all-or-nothing — if any signal is missing or already linked,
        // the whole request is rejected (no partial linking).
```

- [ ] **Step 5: Run tests to verify they pass**

Run: `dotnet test backend/PlateformePFA.Tests/PlateformePFA.Tests.csproj --filter "CreateCase_with_no_owner_falls_back_to_admin|CreateCase_prefers_a_responsable_over_admin"`
Expected: PASS (both).

- [ ] **Step 6: Commit**

```bash
git add backend/PlateformePFA.API/Controllers/InterventionCasesController.cs backend/PlateformePFA.Tests/Controllers/InterventionCasesControllerTests.cs
git commit -m "feat(r1): auto-assign case owner on creation"
```

---

### Task 2: Monitoring-period close guard

Plan rule: "Closed requires the monitoring period to finish." Interpretation: a case carries an optional `FollowUpDate` (set on Resolve) marking the end of monitoring. Closing before that date is blocked; with no `FollowUpDate`, closing is allowed.

**Files:**
- Modify: `backend/PlateformePFA.API/Services/CaseWorkflow.cs` (`CaseFacts` record + `CanTransition`)
- Modify: `backend/PlateformePFA.API/Controllers/InterventionCasesController.cs` (`Transition`, the `CaseFacts` construction ~line 218-222)
- Test: `backend/PlateformePFA.Tests/Services/CaseWorkflowTests.cs`

**Interfaces:**
- Consumes: `InterventionCase.FollowUpDate`.
- Produces: `CaseWorkflow.CaseFacts` gains a fourth member `bool MonitoringComplete`; `CanTransition` rejects `* → Closed` when `MonitoringComplete` is false.

- [ ] **Step 1: Write the failing unit tests**

In `CaseWorkflowTests.cs`, update the `Facts` helper to add the new parameter (default `true`):

```csharp
    private static CaseWorkflow.CaseFacts Facts(bool owner = true, bool outcome = true, bool reason = true, bool monitoring = true)
        => new(HasOwner: owner, HasOutcome: outcome, HasReason: reason, MonitoringComplete: monitoring);
```

Then add:

```csharp
    [Fact]
    public void Close_blocked_until_monitoring_period_finished()
    {
        CaseWorkflow.CanTransition("Resolved", "Closed", Facts(monitoring: false)).ok.Should().BeFalse();
        CaseWorkflow.CanTransition("Resolved", "Closed", Facts(monitoring: true)).ok.Should().BeTrue();
    }
```

- [ ] **Step 2: Run tests to verify they fail to compile/run**

Run: `dotnet test backend/PlateformePFA.Tests/PlateformePFA.Tests.csproj --filter "CaseWorkflowTests"`
Expected: FAIL — `CaseFacts` has no `MonitoringComplete` member (compile error).

- [ ] **Step 3: Add the fact and guard**

In `CaseWorkflow.cs`, change the `CaseFacts` record:

```csharp
        public readonly record struct CaseFacts(bool HasOwner, bool HasOutcome, bool HasReason, bool MonitoringComplete);
```

In `CanTransition`, add this guard immediately after the `Resolved` outcome guard (before the reopen guard):

```csharp
            if (to == CaseWorkflowState.Closed && !facts.MonitoringComplete)
                return (false, "La période de suivi n'est pas terminée ; clôture impossible avant la date de suivi.");
```

- [ ] **Step 4: Update the controller's CaseFacts construction**

In `InterventionCasesController.cs` (`Transition`), replace the `facts` construction:

```csharp
            var facts = new CaseWorkflow.CaseFacts(
                HasOwner:   c.OwnerId.HasValue,
                HasOutcome: !string.IsNullOrWhiteSpace(dto.Outcome ?? c.Outcome)
                            && !string.IsNullOrWhiteSpace(dto.ResolutionSummary ?? c.ResolutionSummary),
                HasReason:  !string.IsNullOrWhiteSpace(dto.Raison));
```

with:

```csharp
            var facts = new CaseWorkflow.CaseFacts(
                HasOwner:   c.OwnerId.HasValue,
                HasOutcome: !string.IsNullOrWhiteSpace(dto.Outcome ?? c.Outcome)
                            && !string.IsNullOrWhiteSpace(dto.ResolutionSummary ?? c.ResolutionSummary),
                HasReason:  !string.IsNullOrWhiteSpace(dto.Raison),
                // Monitoring is "done" when no follow-up date was set, or it has passed.
                MonitoringComplete: c.FollowUpDate == null || c.FollowUpDate <= DateTime.UtcNow);
```

- [ ] **Step 5: Run tests to verify they pass**

Run: `dotnet test backend/PlateformePFA.Tests/PlateformePFA.Tests.csproj --filter "CaseWorkflowTests"`
Expected: PASS (all CaseWorkflow tests, including the existing ones).

- [ ] **Step 6: Commit**

```bash
git add backend/PlateformePFA.API/Services/CaseWorkflow.cs backend/PlateformePFA.API/Controllers/InterventionCasesController.cs backend/PlateformePFA.Tests/Services/CaseWorkflowTests.cs
git commit -m "feat(r1): block case close until monitoring period ends"
```

---

### Task 3: Automatic escalation of critical-overdue cases

Plan rule + test scenario: "Critical overdue cases escalate automatically to Admin." Implement a pure predicate, a scoped sweep service that does the writes, and a `BackgroundService` that runs the sweep every 15 minutes (and once at startup). The worker is disabled under the `Testing` environment so it never races test data; the sweep itself is integration-tested directly.

**Files:**
- Modify: `backend/PlateformePFA.API/Services/CaseWorkflow.cs` (add `IsCriticalOverdue`)
- Create: `backend/PlateformePFA.API/Services/CaseEscalationService.cs`
- Create: `backend/PlateformePFA.API/Services/CaseEscalationWorker.cs`
- Modify: `backend/PlateformePFA.API/Program.cs` (registrations near line 135)
- Test: `backend/PlateformePFA.Tests/Services/CaseWorkflowTests.cs` (unit), `backend/PlateformePFA.Tests/Services/CaseEscalationServiceTests.cs` (integration)

**Interfaces:**
- Consumes: `AppDbContext`, `IAuditService`, `IServiceScopeFactory`, `IHostEnvironment`.
- Produces: `CaseWorkflow.IsCriticalOverdue(string etat, string priorite, DateTime? dueDate, DateTime now) → bool`; `CaseEscalationService.SweepAsync(DateTime now, CancellationToken) → Task<int>` (count escalated).

- [ ] **Step 1: Write the failing unit test for the predicate**

In `CaseWorkflowTests.cs` add:

```csharp
    [Fact]
    public void IsCriticalOverdue_only_for_critical_past_due_active_cases()
    {
        var now = new DateTime(2026, 6, 23, 12, 0, 0, DateTimeKind.Utc);
        var past = now.AddDays(-1);
        var future = now.AddDays(1);

        CaseWorkflow.IsCriticalOverdue("InProgress", "Critical", past, now).Should().BeTrue();
        CaseWorkflow.IsCriticalOverdue("InProgress", "High",     past, now).Should().BeFalse(); // not critical
        CaseWorkflow.IsCriticalOverdue("InProgress", "Critical", future, now).Should().BeFalse(); // not overdue
        CaseWorkflow.IsCriticalOverdue("InProgress", "Critical", null, now).Should().BeFalse(); // no due date
        CaseWorkflow.IsCriticalOverdue("Resolved",   "Critical", past, now).Should().BeFalse(); // terminal
        CaseWorkflow.IsCriticalOverdue("Closed",     "Critical", past, now).Should().BeFalse(); // terminal
        CaseWorkflow.IsCriticalOverdue("Escalated",  "Critical", past, now).Should().BeFalse(); // already escalated
    }
```

- [ ] **Step 2: Run it to verify it fails**

Run: `dotnet test backend/PlateformePFA.Tests/PlateformePFA.Tests.csproj --filter "IsCriticalOverdue_only_for_critical_past_due_active_cases"`
Expected: FAIL — `IsCriticalOverdue` does not exist (compile error).

- [ ] **Step 3: Add the predicate**

In `CaseWorkflow.cs`, add inside the `CaseWorkflow` class (after `CanTransition`):

```csharp
        /// <summary>
        /// True when a case must auto-escalate: Critical priority, past its due
        /// date, and not already in a terminal/escalated state.
        /// </summary>
        public static bool IsCriticalOverdue(string etat, string priorite, DateTime? dueDate, DateTime now)
        {
            if (priorite != "Critical") return false;
            if (etat == CaseWorkflowState.Resolved
                || etat == CaseWorkflowState.Closed
                || etat == CaseWorkflowState.Escalated) return false;
            return dueDate.HasValue && dueDate.Value < now;
        }
```

- [ ] **Step 4: Run it to verify it passes**

Run: `dotnet test backend/PlateformePFA.Tests/PlateformePFA.Tests.csproj --filter "IsCriticalOverdue_only_for_critical_past_due_active_cases"`
Expected: PASS.

- [ ] **Step 5: Write the failing integration test for the sweep**

Create `backend/PlateformePFA.Tests/Services/CaseEscalationServiceTests.cs`:

```csharp
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using PlateformePFA.API.Models;
using PlateformePFA.API.Services;
using PlateformePFA.Tests.Fixtures;
using Xunit;

namespace PlateformePFA.Tests.Services;

public class CaseEscalationServiceTests : IClassFixture<TestWebFactory>
{
    private readonly TestWebFactory _factory;

    public CaseEscalationServiceTests(TestWebFactory factory)
    {
        _factory = factory;
        _factory.SeedAdmin();
        using var ctx = _factory.CreateContext();
        SampleData.SeedOne(ctx);
    }

    [Fact]
    public async Task Sweep_escalates_critical_overdue_and_leaves_others()
    {
        int criticalId, normalId, adminId;
        using (var ctx = _factory.CreateContext())
        {
            var etu = ctx.Etudiants.First(e => e.Matricule == "E10001");
            adminId = ctx.Utilisateurs.Single(u => u.Email == "test-admin@eniad.ma").Id;

            var critical = new InterventionCase
            {
                EtudiantId = etu.Id, FiliereId = etu.FiliereId, Motif = "crit",
                Priorite = "Critical", Etat = CaseWorkflowState.InProgress,
                OwnerId = null, DueDate = DateTime.UtcNow.AddDays(-2),
            };
            var normal = new InterventionCase
            {
                EtudiantId = etu.Id, FiliereId = etu.FiliereId, Motif = "norm",
                Priorite = "High", Etat = CaseWorkflowState.InProgress,
                DueDate = DateTime.UtcNow.AddDays(-2),
            };
            ctx.InterventionCases.AddRange(critical, normal);
            ctx.SaveChanges();
            criticalId = critical.Id; normalId = normal.Id;
        }

        using (var scope = _factory.Services.CreateScope())
        {
            var svc = scope.ServiceProvider.GetRequiredService<CaseEscalationService>();
            var count = await svc.SweepAsync(DateTime.UtcNow);
            count.Should().Be(1);
        }

        using var check = _factory.CreateContext();
        var crit = check.InterventionCases.Single(c => c.Id == criticalId);
        crit.Etat.Should().Be(CaseWorkflowState.Escalated);
        crit.EscaladeLe.Should().NotBeNull();
        crit.OwnerId.Should().Be(adminId);
        check.CaseTimelineEvents.Any(t => t.CaseId == criticalId && t.Action == "Escalated").Should().BeTrue();

        check.InterventionCases.Single(c => c.Id == normalId).Etat.Should().Be(CaseWorkflowState.InProgress);
    }
}
```

- [ ] **Step 6: Run it to verify it fails**

Run: `dotnet test backend/PlateformePFA.Tests/PlateformePFA.Tests.csproj --filter "Sweep_escalates_critical_overdue_and_leaves_others"`
Expected: FAIL — `CaseEscalationService` does not exist (compile error).

- [ ] **Step 7: Create the sweep service**

Create `backend/PlateformePFA.API/Services/CaseEscalationService.cs`:

```csharp
using Microsoft.EntityFrameworkCore;
using PlateformePFA.API.Data;
using PlateformePFA.API.Models;

namespace PlateformePFA.API.Services
{
    /// <summary>
    /// Escalates Critical, past-due, still-active cases to the Escalated state
    /// and reassigns them to an Admin. Idempotent: an already-escalated case is
    /// filtered out, so repeated sweeps are no-ops.
    /// </summary>
    public class CaseEscalationService
    {
        private readonly AppDbContext _db;
        private readonly IAuditService _audit;
        private readonly ILogger<CaseEscalationService> _logger;

        public CaseEscalationService(AppDbContext db, IAuditService audit, ILogger<CaseEscalationService> logger)
        {
            _db = db; _audit = audit; _logger = logger;
        }

        public async Task<int> SweepAsync(DateTime now, CancellationToken ct = default)
        {
            var candidates = await _db.InterventionCases
                .Where(c => c.Priorite == "Critical"
                    && c.Etat != CaseWorkflowState.Resolved
                    && c.Etat != CaseWorkflowState.Closed
                    && c.Etat != CaseWorkflowState.Escalated
                    && c.DueDate != null && c.DueDate < now)
                .ToListAsync(ct);
            if (candidates.Count == 0) return 0;

            var adminId = await _db.Utilisateurs
                .Where(u => u.EstActif && u.Role == "Admin")
                .OrderBy(u => u.Id).Select(u => (int?)u.Id).FirstOrDefaultAsync(ct);

            foreach (var c in candidates)
            {
                var from = c.Etat;
                c.Etat = CaseWorkflowState.Escalated;
                c.EscaladeLe = now;
                if (adminId != null) c.OwnerId = adminId;
                _db.CaseTimelineEvents.Add(new CaseTimelineEvent
                {
                    CaseId = c.Id, Action = "Escalated",
                    Description = $"Escalade automatique : cas critique en retard ({from} → Escalated).",
                    UtilisateurNom = "Système",
                });
            }
            await _db.SaveChangesAsync(ct);

            foreach (var c in candidates)
                await _audit.LogAsync("EscalateCase", "InterventionCase", c.Id,
                    "Escalade automatique (cas critique en retard)", null, "Système");

            _logger.LogInformation("Escalade automatique : {Count} cas escaladé(s).", candidates.Count);
            return candidates.Count;
        }
    }
}
```

- [ ] **Step 8: Register the service and run the integration test**

In `Program.cs`, after the line `builder.Services.AddScoped<IEmailSender, SmtpEmailSender>();` add:

```csharp
builder.Services.AddScoped<CaseEscalationService>();
```

Run: `dotnet test backend/PlateformePFA.Tests/PlateformePFA.Tests.csproj --filter "Sweep_escalates_critical_overdue_and_leaves_others"`
Expected: PASS.

- [ ] **Step 9: Create the background worker**

Create `backend/PlateformePFA.API/Services/CaseEscalationWorker.cs`:

```csharp
namespace PlateformePFA.API.Services
{
    /// <summary>
    /// Runs the escalation sweep at startup and every 15 minutes. Skipped under
    /// the Testing environment so it never races test fixtures.
    /// ponytail: a fixed 15-min timer, not a configurable schedule — add config
    /// only if a real SLA cadence is required.
    /// </summary>
    public class CaseEscalationWorker : BackgroundService
    {
        private static readonly TimeSpan Interval = TimeSpan.FromMinutes(15);
        private readonly IServiceScopeFactory _scopes;
        private readonly IHostEnvironment _env;
        private readonly ILogger<CaseEscalationWorker> _logger;

        public CaseEscalationWorker(IServiceScopeFactory scopes, IHostEnvironment env, ILogger<CaseEscalationWorker> logger)
        {
            _scopes = scopes; _env = env; _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            if (_env.IsEnvironment("Testing")) return;

            using var timer = new PeriodicTimer(Interval);
            try
            {
                do
                {
                    try
                    {
                        using var scope = _scopes.CreateScope();
                        var svc = scope.ServiceProvider.GetRequiredService<CaseEscalationService>();
                        await svc.SweepAsync(DateTime.UtcNow, stoppingToken);
                    }
                    catch (Exception ex) when (ex is not OperationCanceledException)
                    {
                        _logger.LogWarning(ex, "Échec du balayage d'escalade automatique.");
                    }
                } while (await timer.WaitForNextTickAsync(stoppingToken));
            }
            catch (OperationCanceledException) { /* shutdown */ }
        }
    }
}
```

- [ ] **Step 10: Register the worker**

In `Program.cs`, immediately after the `AddScoped<CaseEscalationService>()` line, add:

```csharp
builder.Services.AddHostedService<CaseEscalationWorker>();
```

- [ ] **Step 11: Run the full suite to confirm nothing regressed**

Run: `dotnet test backend/PlateformePFA.Tests/PlateformePFA.Tests.csproj`
Expected: PASS (all tests; the worker is inert under Testing).

- [ ] **Step 12: Commit**

```bash
git add backend/PlateformePFA.API/Services/CaseWorkflow.cs backend/PlateformePFA.API/Services/CaseEscalationService.cs backend/PlateformePFA.API/Services/CaseEscalationWorker.cs backend/PlateformePFA.API/Program.cs backend/PlateformePFA.Tests/Services/CaseWorkflowTests.cs backend/PlateformePFA.Tests/Services/CaseEscalationServiceTests.cs
git commit -m "feat(r1): auto-escalate critical overdue cases to admin"
```

---

### Task 4: Enseignant scoped contribution access

Plan + test scenario: "Teacher contributes module evidence but cannot see private notes or close the case." Today the whole controller is `Admin,Responsable` only. Open the contribution surface (read case, notes, tasks) to Enseignant — scoped to their module cohort — while keeping management actions (create case, transition/close, link/dismiss signals, send/retry email, private notes) privileged-only.

**Files:**
- Modify: `backend/PlateformePFA.API/Controllers/InterventionCasesController.cs` (class attribute, constructor, and per-action authorization + access checks)
- Test: `backend/PlateformePFA.Tests/Controllers/InterventionCasesControllerTests.cs`

**Interfaces:**
- Consumes: `IAcademicAccessService.GetCohortScopeAsync` (returns `CohortScope { Unrestricted, FiliereId, Niveau, HasCohort }`).
- Produces: Enseignant can `GET` cases in their cohort, `GET`/`POST` notes (forced non-private, private hidden) and tasks, and complete tasks; receives `403` on management actions and `404` on out-of-cohort cases.

- [ ] **Step 1: Write the failing tests**

Add to `InterventionCasesControllerTests.cs`. First, a helper that seeds an Enseignant bound to the GI01 module and returns an authed client:

```csharp
private async Task<HttpClient> TeacherClientAsync()
{
    using (var ctx = _factory.CreateContext())
    {
        SampleData.SeedOne(ctx);
        var module = ctx.Modules.Single(m => m.Code == "GI01");
        if (!ctx.Utilisateurs.Any(u => u.Email == "teacher@eniad.ma"))
        {
            ctx.Utilisateurs.Add(new Utilisateur
            {
                Email = "teacher@eniad.ma", Nom = "Teach", Prenom = "Er",
                MotDePasseHash = BCrypt.Net.BCrypt.HashPassword("TeacherPass!2026"),
                Role = "Enseignant", EstActif = true, ModuleId = module.Id,
            });
            ctx.SaveChanges();
        }
    }
    var client = _factory.CreateClient();
    var token = await AuthHelper.GetTokenAsync(client, "teacher@eniad.ma", "TeacherPass!2026");
    client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
    return client;
}
```

Then the tests:

```csharp
[Fact]
public async Task Teacher_can_read_case_and_add_note_but_not_close_or_see_private_notes()
{
    var admin = await AuthedClientAsync();
    int etuId; using (var ctx = _factory.CreateContext()) etuId = StudentId(ctx);
    var caseId = await CreateCaseAsync(admin, etuId);

    // Admin seeds one private note.
    (await admin.PostAsJsonAsync($"/api/intervention-cases/{caseId}/notes",
        new { contenu = "PRIVATE responsable note", isPrivate = true }))
        .EnsureSuccessStatusCode();

    var teacher = await TeacherClientAsync();

    // Read the case: allowed (student is in the teacher's cohort).
    (await teacher.GetAsync($"/api/intervention-cases/{caseId}")).StatusCode
        .Should().Be(HttpStatusCode.OK);

    // Add a note: allowed, and forced non-private.
    var noteRes = await teacher.PostAsJsonAsync($"/api/intervention-cases/{caseId}/notes",
        new { contenu = "Module evidence from teacher", isPrivate = true });
    noteRes.StatusCode.Should().Be(HttpStatusCode.Created);

    // List notes: the private responsable note is hidden; the teacher's own is forced public.
    var notes = await (await teacher.GetAsync($"/api/intervention-cases/{caseId}/notes"))
        .Content.ReadFromJsonAsync<List<CaseNote>>();
    notes!.Should().NotContain(n => n.Contenu.StartsWith("PRIVATE"));
    notes!.Single(n => n.Contenu.StartsWith("Module evidence")).IsPrivate.Should().BeFalse();

    // Close / transition: forbidden.
    (await teacher.PatchAsJsonAsync($"/api/intervention-cases/{caseId}/transition",
        new { etat = "InProgress" })).StatusCode
        .Should().Be(HttpStatusCode.Forbidden);

    // Send communication: forbidden.
    (await teacher.PostAsJsonAsync($"/api/intervention-cases/{caseId}/communications",
        new { templateId = "academic_warning" })).StatusCode
        .Should().Be(HttpStatusCode.Forbidden);
}

[Fact]
public async Task Teacher_cannot_read_case_outside_their_cohort()
{
    var admin = await AuthedClientAsync();
    int outsiderId;
    using (var ctx = _factory.CreateContext())
    {
        // A student in a different niveau than the teacher's CI1 cohort.
        var fil = ctx.Filieres.Single(f => f.Code == "GI");
        var outsider = new Etudiant
        {
            Matricule = "E90001", Nom = "Out", Prenom = "Sider",
            FiliereId = fil.Id, Niveau = "CI3", Annee = "2025/2026",
        };
        ctx.Etudiants.Add(outsider); ctx.SaveChanges();
        outsiderId = outsider.Id;
    }
    var caseId = await CreateCaseAsync(admin, outsiderId);

    var teacher = await TeacherClientAsync();
    (await teacher.GetAsync($"/api/intervention-cases/{caseId}")).StatusCode
        .Should().Be(HttpStatusCode.NotFound);
}
```

Add `using System.Net.Http.Json;` is already present; ensure `using System.Collections.Generic;` and `System.Linq` are available (they are via implicit usings). `PatchAsJsonAsync` lives in `System.Net.Http.Json` (already imported).

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet test backend/PlateformePFA.Tests/PlateformePFA.Tests.csproj --filter "Teacher_can_read_case_and_add_note_but_not_close_or_see_private_notes|Teacher_cannot_read_case_outside_their_cohort"`
Expected: FAIL — teacher currently gets `403` on every endpoint (class-level role restriction), so the `GET` and note assertions fail.

- [ ] **Step 3: Inject the access service**

In `InterventionCasesController.cs`, change the class attribute from:

```csharp
    [Authorize(Roles = "Admin,Responsable")]
    [Route("api/intervention-cases")]
```

to:

```csharp
    [Authorize]
    [Route("api/intervention-cases")]
```

Add the field and constructor parameter. Change the field block:

```csharp
        private readonly AppDbContext _context;
        private readonly IAuditService _audit;
        private readonly IEmailSender _email;
        private readonly ILogger<InterventionCasesController> _logger;

        public InterventionCasesController(AppDbContext context, IAuditService audit, IEmailSender email, ILogger<InterventionCasesController> logger)
        {
            _context = context;
            _audit   = audit;
            _email   = email;
            _logger  = logger;
        }
```

to:

```csharp
        private readonly AppDbContext _context;
        private readonly IAuditService _audit;
        private readonly IEmailSender _email;
        private readonly IAcademicAccessService _academicAccess;
        private readonly ILogger<InterventionCasesController> _logger;

        public InterventionCasesController(AppDbContext context, IAuditService audit, IEmailSender email, IAcademicAccessService academicAccess, ILogger<InterventionCasesController> logger)
        {
            _context = context;
            _audit   = audit;
            _email   = email;
            _academicAccess = academicAccess;
            _logger  = logger;
        }
```

- [ ] **Step 4: Add access helpers**

In `InterventionCasesController.cs`, add next to `CurrentUser`:

```csharp
        private bool IsPrivileged() => User.IsInRole("Admin") || User.IsInRole("Responsable");

        // A teacher may only touch a case whose student is in their module cohort.
        // Admin/Responsable are unrestricted. Returns false (caller maps to 404)
        // for an out-of-cohort or unresolved-cohort teacher.
        private async Task<bool> CanAccessCaseAsync(int etudiantId)
        {
            var cohort = await _academicAccess.GetCohortScopeAsync(User);
            if (cohort.Unrestricted) return true;
            if (!cohort.HasCohort) return false;
            var etu = await _context.Etudiants.AsNoTracking()
                .FirstOrDefaultAsync(e => e.Id == etudiantId);
            return etu != null && etu.FiliereId == cohort.FiliereId && etu.Niveau == cohort.Niveau;
        }
```

- [ ] **Step 5: Restrict the management actions to privileged roles**

Add `[Authorize(Roles = "Admin,Responsable")]` directly above each of these action methods: `CreateCase`, `LinkSignals`, `Transition`, `SendCommunication`, `RetryCommunication`. Example for `CreateCase`:

```csharp
        // POST: api/intervention-cases
        [Authorize(Roles = "Admin,Responsable")]
        [HttpPost]
        public async Task<ActionResult<InterventionCase>> CreateCase(CreateCaseDto dto)
```

(Repeat the attribute line above `LinkSignals`, `Transition`, `SendCommunication`, and `RetryCommunication`.)

- [ ] **Step 6: Scope the list and gate the per-case reads**

In `GetCases`, after the `etudiantId` filter and before `var ordered = ...`, add cohort scoping:

```csharp
            // Teachers see only cases for students in their module cohort.
            var cohort = await _academicAccess.GetCohortScopeAsync(User);
            if (!cohort.Unrestricted)
            {
                if (!cohort.HasCohort)
                    return new PaginatedResult<InterventionCase>(new List<InterventionCase>(), 0, page, pageSize);
                var cohortStudentIds = _context.Etudiants
                    .Where(e => e.FiliereId == cohort.FiliereId && e.Niveau == cohort.Niveau)
                    .Select(e => e.Id);
                query = query.Where(c => cohortStudentIds.Contains(c.EtudiantId));
            }
```

In `GetCase`, after loading `c` and the null check, add:

```csharp
            if (!await CanAccessCaseAsync(c.EtudiantId))
                return NotFound(new { message = "Cas introuvable." });
```

For the per-case sub-resource reads/writes that currently only check existence with `AnyAsync` — `GetTasks`, `CreateTask`, `GetNotes`, `CreateNote`, `GetCommunications`, and `CompleteTask` — replace the existence check so it also enforces cohort access. For each of `GetTasks`, `CreateTask`, `GetNotes`, `CreateNote`, `GetCommunications`, replace:

```csharp
            if (!await _context.InterventionCases.AnyAsync(c => c.Id == id))
                return NotFound(new { message = "Cas introuvable." });
```

with:

```csharp
            var owningEtudiantId = await _context.InterventionCases
                .Where(c => c.Id == id).Select(c => (int?)c.EtudiantId).FirstOrDefaultAsync();
            if (owningEtudiantId == null || !await CanAccessCaseAsync(owningEtudiantId.Value))
                return NotFound(new { message = "Cas introuvable." });
```

In `CompleteTask`, after loading `task` and its null check, add:

```csharp
            if (!await CanAccessCaseAsync(
                    await _context.InterventionCases.Where(c => c.Id == id)
                        .Select(c => c.EtudiantId).FirstAsync()))
                return NotFound(new { message = "Cas introuvable." });
```

- [ ] **Step 7: Hide private notes and force teacher notes public**

In `GetNotes`, replace the return query so non-privileged callers don't see private notes. Change:

```csharp
            return await _context.CaseNotes.AsNoTracking()
                .Where(n => n.CaseId == id)
                .OrderByDescending(n => n.CreeLe)
                .ToListAsync();
```

to:

```csharp
            var notesQuery = _context.CaseNotes.AsNoTracking().Where(n => n.CaseId == id);
            if (!IsPrivileged()) notesQuery = notesQuery.Where(n => !n.IsPrivate);
            return await notesQuery.OrderByDescending(n => n.CreeLe).ToListAsync();
```

In `CreateNote`, force non-privileged callers' notes to be public. Change:

```csharp
                IsPrivate = dto.IsPrivate,
```

to:

```csharp
                IsPrivate = IsPrivileged() && dto.IsPrivate,
```

- [ ] **Step 8: Run the teacher tests to verify they pass**

Run: `dotnet test backend/PlateformePFA.Tests/PlateformePFA.Tests.csproj --filter "Teacher_can_read_case_and_add_note_but_not_close_or_see_private_notes|Teacher_cannot_read_case_outside_their_cohort"`
Expected: PASS (both).

- [ ] **Step 9: Run the full suite to confirm no regression**

Run: `dotnet test backend/PlateformePFA.Tests/PlateformePFA.Tests.csproj`
Expected: PASS (all tests — existing Admin-path tests still pass because Admin is unrestricted).

- [ ] **Step 10: Commit**

```bash
git add backend/PlateformePFA.API/Controllers/InterventionCasesController.cs backend/PlateformePFA.Tests/Controllers/InterventionCasesControllerTests.cs
git commit -m "feat(r1): scoped Enseignant case contribution access"
```

---

## Self-review notes

- **Spec coverage:** Task 1 → auto-assign rule; Task 2 → "Closed requires monitoring period"; Task 3 → "Critical overdue cases escalate automatically to Admin"; Task 4 → teacher contribution + private-note privacy + "teachers cannot close cases." The Copilot scope mismatch is a human decision flagged in Pre-flight, not a code task.
- **Type consistency:** `CaseFacts` gains `MonitoringComplete` in Task 2 and every construction site (the one in `Transition`) is updated in the same task. `CaseEscalationService.SweepAsync` / `CaseWorkflow.IsCriticalOverdue` signatures are used identically in their tests.
- **Out of scope (deliberate):** no frontend changes (the gaps are all backend behavior; the existing pages already call these endpoints); no new SMTP/email work; no DB migration needed (escalation reuses existing columns `Etat`, `EscaladeLe`, `OwnerId`).
