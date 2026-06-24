# Student Outreach Intervention Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Turn a high-risk alert into a human-approved student email, a scheduled meeting, and a recorded meeting outcome.

**Architecture:** Extend the existing `InterventionCase` and `CaseCommunication` records, but isolate the new workflow in a focused `CaseOutreachController`. The backend remains authoritative for roles, transitions, delivery state, and auditing; the Copilot runtime only generates an optional structured draft, and the frontend always offers a deterministic French template fallback.

**Tech Stack:** ASP.NET Core 8, EF Core, SQL Server runtime migrations, xUnit/FluentAssertions, React 18, TypeScript, TanStack Query, Vitest/Testing Library, Express, NVIDIA NIM via AI SDK, Playwright.

---

## File Structure

### Backend

- Modify `backend/PlateformePFA.API/Models/InterventionCase.cs` — persist the single scheduled outreach meeting and attendance.
- Modify `backend/PlateformePFA.API/Models/CaseCommunication.cs` — support editable drafts and optimistic delivery states.
- Create `backend/PlateformePFA.API/DTOs/Interventions/OutreachDtos.cs` — define draft, edit, schedule/send, and meeting-result contracts.
- Create `backend/PlateformePFA.API/Controllers/CaseOutreachController.cs` — own the outreach workflow without expanding the general case controller.
- Modify `backend/PlateformePFA.API/Data/RuntimeMigrations.cs` — roll existing and fresh databases forward idempotently.
- Modify `backend/PlateformePFA.API/Services/EmailTemplates.cs` — provide the deterministic meeting invitation fallback.
- Modify `backend/PlateformePFA.API/Controllers/InterventionCasesController.cs` — prevent duplicate active interventions and keep legacy communication routes from bypassing outreach rules.
- Modify `backend/PlateformePFA.API/DTOs/Dashboard/InterventionDashboardDto.cs` and `backend/PlateformePFA.API/Controllers/DashboardController.cs` — expose the outreach funnel.
- Test `backend/PlateformePFA.Tests/Controllers/StudentOutreachControllerTests.cs` — exercise authorization, validation, delivery, retries, transitions, and audit history.
- Modify `backend/PlateformePFA.Tests/Controllers/InterventionCasesControllerTests.cs` — cover duplicate active-case rejection.

### Copilot runtime

- Create `copilot-runtime/src/outreach.ts` — validate the minimal prompt payload and parse the structured NIM response.
- Create `copilot-runtime/src/outreach.test.ts` — test prompt privacy, output parsing, and failure behavior without network calls.
- Modify `copilot-runtime/src/index.ts` — expose the authenticated `/api/outreach/draft` endpoint.
- Modify `copilot-runtime/package.json` and `copilot-runtime/package-lock.json` — make the already-resolved `ai` dependency explicit.

### Frontend

- Modify `frontend/src/services/interventions.ts` — add outreach types and API calls.
- Create `frontend/src/services/outreachDraft.ts` — call AI drafting and provide the deterministic fallback.
- Create `frontend/src/services/outreachDraft.test.ts` — test fallback and response validation.
- Modify `frontend/src/pages/Cases.tsx` — present the four-stage Intervention queue.
- Create `frontend/src/pages/Cases.test.tsx` — test stage mapping, filters, and navigation.
- Create `frontend/src/components/interventions/OutreachComposer.tsx` — edit, review, schedule, send, retry, and show unknown delivery states.
- Create `frontend/src/components/interventions/MeetingOutcomeForm.tsx` — record held/absent/cancelled outcomes.
- Modify `frontend/src/pages/CaseDetail.tsx` — compose the outreach components and remove manual state buttons for the primary outreach path.
- Create `frontend/src/pages/CaseDetail.test.tsx` — test role restrictions and stage progression.
- Modify `frontend/src/pages/Triage.tsx` — make Start intervention the primary action and handle HTTP 409 duplicate redirection.
- Modify `frontend/e2e/smoke.spec.ts` — demonstrate alert-to-held-meeting behavior.

## Task 1: Persist Outreach State Safely

**Files:**
- Modify: `backend/PlateformePFA.API/Models/InterventionCase.cs`
- Modify: `backend/PlateformePFA.API/Models/CaseCommunication.cs`
- Modify: `backend/PlateformePFA.API/Data/RuntimeMigrations.cs`
- Test: `backend/PlateformePFA.Tests/Controllers/StudentOutreachControllerTests.cs`

- [ ] **Step 1: Write a failing persistence contract test**

Create the test file with this initial test:

```csharp
public class StudentOutreachControllerTests : IClassFixture<TestWebFactory>
{
    private readonly TestWebFactory _factory;
    public StudentOutreachControllerTests(TestWebFactory factory) => _factory = factory;

    [Fact]
    public void Outreach_fields_are_mapped_by_ef()
    {
        using var db = _factory.CreateContext();
        var entity = db.Model.FindEntityType(typeof(InterventionCase))!;
        entity.FindProperty("MeetingScheduledFor").Should().NotBeNull();
        entity.FindProperty("MeetingLocation").Should().NotBeNull();
        entity.FindProperty("MeetingAttendance").Should().NotBeNull();
        entity.FindProperty("MeetingHeldAt").Should().NotBeNull();
    }
}
```

- [ ] **Step 2: Run the test and verify failure**

Run: `dotnet test backend/PlateformePFA.Tests --filter "Outreach_fields_are_mapped_by_ef" --nologo`

Expected: FAIL because `MeetingScheduledFor` is not mapped.

- [ ] **Step 3: Add the model fields and centralized status constants**

Add to `InterventionCase`:

```csharp
public DateTime? MeetingScheduledFor { get; set; }
[MaxLength(200)] public string? MeetingLocation { get; set; }
[MaxLength(20)] public string? MeetingAttendance { get; set; } // Held | Absent | Cancelled
public DateTime? MeetingHeldAt { get; set; }
```

Add to `CaseCommunication.cs`:

```csharp
public static class CommunicationStatus
{
    public const string Draft = "Draft";
    public const string Queued = "Queued";
    public const string Sent = "Sent";
    public const string Failed = "Failed";
}
```

Keep `Status` defaulting to `Queued` for legacy generic communication creation; the new outreach endpoint explicitly creates `Draft`.

- [ ] **Step 4: Add idempotent SQL migration blocks**

Append guarded `ALTER TABLE` statements to `RuntimeMigrations.Apply`:

```sql
IF OBJECT_ID('dbo.InterventionCases', 'U') IS NOT NULL
   AND COL_LENGTH('dbo.InterventionCases', 'MeetingScheduledFor') IS NULL
    ALTER TABLE InterventionCases ADD MeetingScheduledFor DATETIME2 NULL;
IF OBJECT_ID('dbo.InterventionCases', 'U') IS NOT NULL
   AND COL_LENGTH('dbo.InterventionCases', 'MeetingLocation') IS NULL
    ALTER TABLE InterventionCases ADD MeetingLocation NVARCHAR(200) NULL;
IF OBJECT_ID('dbo.InterventionCases', 'U') IS NOT NULL
   AND COL_LENGTH('dbo.InterventionCases', 'MeetingAttendance') IS NULL
    ALTER TABLE InterventionCases ADD MeetingAttendance NVARCHAR(20) NULL;
IF OBJECT_ID('dbo.InterventionCases', 'U') IS NOT NULL
   AND COL_LENGTH('dbo.InterventionCases', 'MeetingHeldAt') IS NULL
    ALTER TABLE InterventionCases ADD MeetingHeldAt DATETIME2 NULL;
```

Drop and recreate `CK_CaseCommunications_Status` with `Draft`, `Queued`, `Sent`, and `Failed`, then add `CK_InterventionCases_MeetingAttendance` allowing `NULL`, `Held`, `Absent`, and `Cancelled`. Guard both operations by constraint name.

- [ ] **Step 5: Run backend tests and commit**

Run: `dotnet test backend/PlateformePFA.Tests --nologo`

Expected: PASS.

```bash
git add backend/PlateformePFA.API/Models/InterventionCase.cs backend/PlateformePFA.API/Models/CaseCommunication.cs backend/PlateformePFA.API/Data/RuntimeMigrations.cs backend/PlateformePFA.Tests/Controllers/StudentOutreachControllerTests.cs
git commit -m "feat(outreach): persist meeting and draft state"
```

## Task 2: Prevent Duplicate Active Interventions

**Files:**
- Modify: `backend/PlateformePFA.API/Controllers/InterventionCasesController.cs`
- Modify: `backend/PlateformePFA.Tests/Controllers/InterventionCasesControllerTests.cs`
- Modify: `frontend/src/pages/Triage.tsx`

- [ ] **Step 1: Write the failing HTTP test**

```csharp
[Fact]
public async Task Create_case_returns_conflict_with_existing_active_case()
{
    var client = await AuthedClientAsync();
    using var db = _factory.CreateContext();
    var studentId = StudentId(db);
    var existingId = await CreateCaseAsync(client, studentId);

    var response = await client.PostAsJsonAsync("/api/intervention-cases", new
    {
        etudiantId = studentId,
        motif = "Second outreach",
        priorite = "High"
    });

    response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    var body = await response.Content.ReadFromJsonAsync<JsonElement>();
    body.GetProperty("existingCaseId").GetInt32().Should().Be(existingId);
}
```

- [ ] **Step 2: Run the test and verify failure**

Run: `dotnet test backend/PlateformePFA.Tests --filter "Create_case_returns_conflict" --nologo`

Expected: FAIL because a second case is currently created.

- [ ] **Step 3: Add the duplicate guard before constructing the case**

```csharp
var terminal = new[] { CaseWorkflowState.Resolved, CaseWorkflowState.Closed };
var existingCaseId = await _context.InterventionCases.AsNoTracking()
    .Where(c => c.EtudiantId == dto.EtudiantId && !terminal.Contains(c.Etat))
    .OrderByDescending(c => c.CreeLe)
    .Select(c => (int?)c.Id)
    .FirstOrDefaultAsync();
if (existingCaseId.HasValue)
    return Conflict(new
    {
        message = "Une intervention active existe déjà pour cet étudiant.",
        existingCaseId = existingCaseId.Value,
    });
```

- [ ] **Step 4: Handle conflict in Triage without creating another record**

In the existing create mutation, inspect `AxiosError<{ existingCaseId?: number }>` and navigate to `/cases/{existingCaseId}` when status is 409. Keep other failures in the existing error dialog.

```tsx
onError: (error: AxiosError<{ existingCaseId?: number }>) => {
  const existing = error.response?.data.existingCaseId
  if (error.response?.status === 409 && existing) navigate(`/cases/${existing}`)
  else setErrorMessage('Impossible de démarrer cette intervention.')
}
```

- [ ] **Step 5: Run backend and frontend tests and commit**

Run: `dotnet test backend/PlateformePFA.Tests --nologo`

Run: `npm test -- --run` from `frontend`

Expected: PASS.

```bash
git add backend/PlateformePFA.API/Controllers/InterventionCasesController.cs backend/PlateformePFA.Tests/Controllers/InterventionCasesControllerTests.cs frontend/src/pages/Triage.tsx
git commit -m "feat(outreach): prevent duplicate active interventions"
```

## Task 3: Create and Edit a Persisted Email Draft

**Files:**
- Create: `backend/PlateformePFA.API/DTOs/Interventions/OutreachDtos.cs`
- Create: `backend/PlateformePFA.API/Controllers/CaseOutreachController.cs`
- Modify: `backend/PlateformePFA.API/Services/EmailTemplates.cs`
- Test: `backend/PlateformePFA.Tests/Controllers/StudentOutreachControllerTests.cs`

- [ ] **Step 1: Add failing draft lifecycle tests**

Test that Admin/Responsable can create and edit one Draft, that an Enseignant receives 403, and that subject/body edits append `EmailDraftEdited` to the timeline.

```csharp
var create = await client.PostAsJsonAsync($"/api/intervention-cases/{caseId}/outreach/draft", new
{
    subject = "Invitation à un entretien",
    body = "Bonjour, rencontrons-nous pour faire le point."
});
create.StatusCode.Should().Be(HttpStatusCode.Created);
var draft = await create.Content.ReadFromJsonAsync<CaseCommunication>();
draft!.Status.Should().Be(CommunicationStatus.Draft);

var edit = await client.PutAsJsonAsync(
    $"/api/intervention-cases/{caseId}/outreach/draft/{draft.Id}",
    new { subject = "Entretien ENIAD", body = "Bonjour, voici la version relue." });
edit.StatusCode.Should().Be(HttpStatusCode.NoContent);
```

- [ ] **Step 2: Run tests and verify 404 failure**

Run: `dotnet test backend/PlateformePFA.Tests --filter "FullyQualifiedName~StudentOutreachControllerTests" --nologo`

Expected: FAIL because the outreach route does not exist.

- [ ] **Step 3: Define exact request contracts**

```csharp
public sealed class SaveOutreachDraftDto
{
    [Required, MaxLength(300)] public string Subject { get; set; } = string.Empty;
    [Required, MaxLength(4000)] public string Body { get; set; } = string.Empty;
}

public sealed class ScheduleAndSendDto
{
    public DateTime ScheduledFor { get; set; }
    [Required, MaxLength(200)] public string Location { get; set; } = string.Empty;
}

public sealed class RecordMeetingDto
{
    [Required] public string Attendance { get; set; } = string.Empty;
    public DateTime? HeldAt { get; set; }
    [MaxLength(40)] public string? Outcome { get; set; }
    [MaxLength(1000)] public string? Summary { get; set; }
}
```

- [ ] **Step 4: Implement the focused controller's draft endpoints**

Use `[Authorize(Roles = "Admin,Responsable")]` and route `api/intervention-cases/{caseId:int}/outreach`. On create:

```csharp
var c = await _db.InterventionCases.Include(x => x.Etudiant)
    .SingleOrDefaultAsync(x => x.Id == caseId);
if (c == null) return NotFound();
if (string.IsNullOrWhiteSpace(c.Etudiant.Email))
    return BadRequest(new { message = "L'étudiant ne possède pas d'adresse email." });
if (await _db.CaseCommunications.AnyAsync(x => x.CaseId == caseId && x.Status == CommunicationStatus.Draft))
    return Conflict(new { message = "Un brouillon existe déjà pour cette intervention." });

var comm = new CaseCommunication
{
    CaseId = caseId,
    Destinataire = c.Etudiant.Email,
    TemplateId = "student_outreach",
    Sujet = dto.Subject.Trim(),
    Corps = dto.Body.Trim(),
    Status = CommunicationStatus.Draft,
    CreeParId = userId,
};
c.Etat = CaseWorkflowState.InProgress;
```

Append `EmailDraftCreated`; edit only when status is `Draft`, append `EmailDraftEdited`, save, and audit both operations. Add a `meeting_outreach` template whose subject/body include `{{Date}}`, `{{Time}}`, and `{{Location}}`; extend `EmailTemplates.Render` with a separate `RenderMeeting` method rather than changing existing callers.

- [ ] **Step 5: Run tests and commit**

Run: `dotnet test backend/PlateformePFA.Tests --filter "FullyQualifiedName~StudentOutreachControllerTests" --nologo`

Expected: PASS.

```bash
git add backend/PlateformePFA.API/DTOs/Interventions/OutreachDtos.cs backend/PlateformePFA.API/Controllers/CaseOutreachController.cs backend/PlateformePFA.API/Services/EmailTemplates.cs backend/PlateformePFA.Tests/Controllers/StudentOutreachControllerTests.cs
git commit -m "feat(outreach): add editable email drafts"
```

## Task 4: Schedule the Meeting and Send Exactly Once

**Files:**
- Modify: `backend/PlateformePFA.API/Controllers/CaseOutreachController.cs`
- Modify: `backend/PlateformePFA.API/Controllers/InterventionCasesController.cs`
- Test: `backend/PlateformePFA.Tests/Controllers/StudentOutreachControllerTests.cs`

- [ ] **Step 1: Write failing send-state tests**

Cover future-date validation, successful delivery, failed delivery, retry, and `Queued` duplicate-send protection. The key assertion is:

```csharp
saved.MeetingScheduledFor.Should().Be(requestedUtc);
saved.MeetingLocation.Should().Be("Salle B12");
saved.Etat.Should().Be(CaseWorkflowState.WaitingStudent);
communication.Status.Should().Be(CommunicationStatus.Sent);
timeline.Should().Contain(x => x.Action == "MeetingScheduled");
timeline.Should().Contain(x => x.Action == "EmailSent");
```

For a failed sender, assert `Etat == InProgress`, `Status == Failed`, and the draft text remains unchanged.

- [ ] **Step 2: Run tests and verify failure**

Run: `dotnet test backend/PlateformePFA.Tests --filter "FullyQualifiedName~StudentOutreachControllerTests" --nologo`

Expected: FAIL because schedule/send does not exist.

- [ ] **Step 3: Implement validation and the delivery-state guard**

Reject `ScheduledFor <= DateTime.UtcNow`, blank location, non-Draft/non-Failed communication, or a case outside `InProgress`. Before SMTP, persist:

```csharp
c.MeetingScheduledFor = dto.ScheduledFor.ToUniversalTime();
c.MeetingLocation = dto.Location.Trim();
comm.Status = CommunicationStatus.Queued;
comm.Erreur = null;
await _db.SaveChangesAsync();
```

`Queued` is deliberately not retryable: it means delivery is running or its outcome is unknown. This prevents a second click from sending a duplicate message.

- [ ] **Step 4: Persist the authoritative SMTP result and transition only on success**

```csharp
var (ok, error) = await _email.SendAsync(comm.Destinataire, comm.Sujet, comm.Corps);
comm.Status = ok ? CommunicationStatus.Sent : CommunicationStatus.Failed;
comm.Erreur = error;
if (ok)
{
    comm.EnvoyeLe = DateTime.UtcNow;
    c.Etat = CaseWorkflowState.WaitingStudent;
}
```

Append `MeetingScheduled` before the attempt and `EmailSent` or `EmailFailed` after it. Retry accepts only `Failed`, reuses the stored text and stored meeting details, and transitions only after success. Make the legacy generic send endpoint reject `TemplateId == "student_outreach"` so it cannot bypass this flow.

- [ ] **Step 5: Run tests and commit**

Run: `dotnet test backend/PlateformePFA.Tests --nologo`

Expected: PASS.

```bash
git add backend/PlateformePFA.API/Controllers/CaseOutreachController.cs backend/PlateformePFA.API/Controllers/InterventionCasesController.cs backend/PlateformePFA.Tests/Controllers/StudentOutreachControllerTests.cs
git commit -m "feat(outreach): schedule and send student invitations"
```

## Task 5: Record Meeting Attendance and Complete Correctly

**Files:**
- Modify: `backend/PlateformePFA.API/Controllers/CaseOutreachController.cs`
- Test: `backend/PlateformePFA.Tests/Controllers/StudentOutreachControllerTests.cs`

- [ ] **Step 1: Write failing meeting-result tests**

Create parameterized tests proving `Absent` and `Cancelled` remain `WaitingStudent`, while `Held` requires a non-future held time, outcome, and summary before resolving.

```csharp
private async Task<(HttpClient client, int caseId)> CreateScheduledOutreachAsync()
{
    var client = await AuthedClientAsync();
    using var db = _factory.CreateContext();
    var studentId = StudentId(db);
    var caseId = await CreateCaseAsync(client, studentId);
    var draftResponse = await client.PostAsJsonAsync(
        $"/api/intervention-cases/{caseId}/outreach/draft",
        new { subject = "Invitation", body = "Bonjour, rendez-vous à l'ENIAD." });
    var draft = await draftResponse.Content.ReadFromJsonAsync<CaseCommunication>();
    await client.PostAsJsonAsync(
        $"/api/intervention-cases/{caseId}/outreach/draft/{draft!.Id}/send",
        new { scheduledFor = DateTime.UtcNow.AddDays(2), location = "Salle B12" });
    return (client, caseId);
}

[Theory]
[InlineData("Absent")]
[InlineData("Cancelled")]
public async Task Non_held_meeting_stays_active(string attendance)
{
    var (client, caseId) = await CreateScheduledOutreachAsync();
    var response = await client.PostAsJsonAsync(
        $"/api/intervention-cases/{caseId}/outreach/meeting-result",
        new { attendance });
    response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    using var verify = _factory.CreateContext();
    var saved = verify.InterventionCases.Single(x => x.Id == caseId);
    saved.Etat.Should().Be(CaseWorkflowState.WaitingStudent);
    saved.MeetingAttendance.Should().Be(attendance);
    saved.MeetingHeldAt.Should().BeNull();
}

[Fact]
public async Task Held_meeting_requires_outcome_and_resolves()
{
    var (client, caseId) = await CreateScheduledOutreachAsync();
    var heldAt = DateTime.UtcNow.AddMinutes(-10);
    var invalid = await client.PostAsJsonAsync(
        $"/api/intervention-cases/{caseId}/outreach/meeting-result",
        new { attendance = "Held", heldAt, outcome = "Improved", summary = "" });
    invalid.StatusCode.Should().Be(HttpStatusCode.BadRequest);

    var valid = await client.PostAsJsonAsync(
        $"/api/intervention-cases/{caseId}/outreach/meeting-result",
        new { attendance = "Held", heldAt, outcome = "Improved", summary = "Entretien réalisé." });
    valid.StatusCode.Should().Be(HttpStatusCode.NoContent);
    using var verify = _factory.CreateContext();
    var saved = verify.InterventionCases.Single(x => x.Id == caseId);
    saved.Etat.Should().Be(CaseWorkflowState.Resolved);
    saved.MeetingAttendance.Should().Be("Held");
    saved.ResolutionSummary.Should().Be("Entretien réalisé.");
}
```

- [ ] **Step 2: Run tests and verify failure**

Run: `dotnet test backend/PlateformePFA.Tests --filter "Non_held_meeting|Held_meeting" --nologo`

Expected: FAIL because the result endpoint does not exist.

- [ ] **Step 3: Implement explicit attendance rules**

Add `POST /api/intervention-cases/{caseId}/outreach/meeting-result`:

```csharp
var allowed = new[] { "Held", "Absent", "Cancelled" };
if (!allowed.Contains(dto.Attendance)) return BadRequest(new { message = "Présence invalide." });
if (dto.Attendance == "Held" &&
    (!dto.HeldAt.HasValue || dto.HeldAt > DateTime.UtcNow ||
     string.IsNullOrWhiteSpace(dto.Outcome) || string.IsNullOrWhiteSpace(dto.Summary)))
    return BadRequest(new { message = "La date, le résultat et le résumé sont requis." });
```

For `Held`, set attendance, held time, outcome, summary, `Etat = Resolved`, and append `MeetingHeld`. For `Absent` or `Cancelled`, clear `MeetingHeldAt`, keep `WaitingStudent`, append `MeetingAbsent` or `MeetingCancelled`, and require a later reschedule/send action before resolution.

- [ ] **Step 4: Assert authorization and audit attribution**

Add a teacher request expecting 403 and assert the successful Responsable event has both `UtilisateurId` and `UtilisateurNom`.

- [ ] **Step 5: Run tests and commit**

Run: `dotnet test backend/PlateformePFA.Tests --nologo`

Expected: PASS.

```bash
git add backend/PlateformePFA.API/Controllers/CaseOutreachController.cs backend/PlateformePFA.Tests/Controllers/StudentOutreachControllerTests.cs
git commit -m "feat(outreach): record meeting attendance and outcomes"
```

## Task 6: Add Optional AI Drafting With a Safe Fallback

**Files:**
- Create: `copilot-runtime/src/outreach.ts`
- Create: `copilot-runtime/src/outreach.test.ts`
- Modify: `copilot-runtime/src/index.ts`
- Modify: `copilot-runtime/package.json`
- Modify: `copilot-runtime/package-lock.json`
- Create: `frontend/src/services/outreachDraft.ts`
- Create: `frontend/src/services/outreachDraft.test.ts`

- [ ] **Step 1: Write runtime unit tests around a fake generator**

```ts
it("rejects output that exposes a risk score", async () => {
  const generate = vi.fn().mockResolvedValue({
    text: JSON.stringify({ subject: "Entretien", body: "Votre risque est 87%." }),
  });
  await expect(createOutreachDraft(validInput, generate)).rejects.toThrow("unsafe draft");
});

it("returns a short French draft", async () => {
  const generate = vi.fn().mockResolvedValue({
    text: JSON.stringify({ subject: "Invitation à un entretien", body: "Bonjour Sara…" }),
  });
  await expect(createOutreachDraft(validInput, generate)).resolves.toMatchObject({ subject: expect.any(String) });
});
```

- [ ] **Step 2: Run runtime tests and verify failure**

Run: `npm test -- outreach.test.ts` from `copilot-runtime`

Expected: FAIL because `outreach.ts` does not exist.

- [ ] **Step 3: Implement a minimal, privacy-bounded generator**

Define the accepted input with Zod:

```ts
export const outreachInput = z.object({
  firstName: z.string().min(1).max(80),
  concernSummary: z.string().min(1).max(500),
  scheduledFor: z.string().datetime(),
  location: z.string().min(1).max(200),
});

export const outreachOutput = z.object({
  subject: z.string().min(1).max(300),
  body: z.string().min(1).max(4000),
});
```

Prompt the model to return JSON only, in supportive French, and prohibit scores, labels, private notes, accusations, and diagnoses. Parse with `outreachOutput`; reject bodies matching `/\b\d{1,3}\s*%|score de risque|haut risque/i`. Export the pure `createOutreachDraft(input, generate)` function for tests.

- [ ] **Step 4: Add authenticated endpoint and frontend fallback**

Mount the same cookie verification used by `/api/copilotkit` on `/api/outreach`. Add `POST /api/outreach/draft`, call `generateText` with `nim.chat(process.env.COPILOT_MODEL_ROUTER ?? "meta/llama-3.3-70b-instruct")`, and return 422 for invalid model output and 503 for provider failure.

In `outreachDraft.ts`, export:

```ts
export function fallbackOutreachDraft(input: OutreachDraftInput): OutreachDraft {
  const when = new Date(input.scheduledFor).toLocaleString('fr-FR', { dateStyle: 'long', timeStyle: 'short' })
  return {
    subject: 'Invitation à un entretien — ENIAD',
    body: `Bonjour ${input.firstName},\n\nNous souhaitons vous rencontrer afin de faire le point sur votre parcours et vous proposer un accompagnement adapté.\n\nRendez-vous : ${when}\nLieu : ${input.location}\n\nCordialement,\nL'équipe pédagogique ENIAD`,
  }
}
```

`generateOutreachDraft` calls `/api/outreach/draft` with credentials and returns the fallback on 401, 422, 503, timeout, or invalid response. It never falls back after the email has entered a send state.

- [ ] **Step 5: Run tests and commit**

Run: `npm test` from `copilot-runtime`

Run: `npm test -- --run` from `frontend`

Expected: PASS.

```bash
git add copilot-runtime/src/outreach.ts copilot-runtime/src/outreach.test.ts copilot-runtime/src/index.ts copilot-runtime/package.json copilot-runtime/package-lock.json frontend/src/services/outreachDraft.ts frontend/src/services/outreachDraft.test.ts
git commit -m "feat(outreach): generate safe French email drafts"
```

## Task 7: Add Typed Frontend Outreach APIs

**Files:**
- Modify: `frontend/src/services/interventions.ts`
- Test: `frontend/src/services/studentImport.test.ts` or create `frontend/src/services/interventions.test.ts`

- [ ] **Step 1: Add failing API contract tests**

Mock `api` and assert exact paths and bodies for create draft, edit draft, schedule/send, retry, and meeting result.

```ts
await scheduleAndSendOutreach(12, 7, { scheduledFor, location: 'Salle B12' })
expect(api.post).toHaveBeenCalledWith(
  '/intervention-cases/12/outreach/draft/7/send',
  { scheduledFor, location: 'Salle B12' },
)
```

- [ ] **Step 2: Run the service test and verify failure**

Run: `npm test -- interventions.test.ts` from `frontend`

Expected: FAIL because the exports do not exist.

- [ ] **Step 3: Extend types without weakening existing contracts**

Add meeting fields to `InterventionCase`, add `Draft` to `CaseCommunication.status`, and export:

```ts
export type MeetingAttendance = 'Held' | 'Absent' | 'Cancelled'
export interface SaveDraftBody { subject: string; body: string }
export interface ScheduleAndSendBody { scheduledFor: string; location: string }
export interface MeetingResultBody {
  attendance: MeetingAttendance
  heldAt?: string
  outcome?: string
  summary?: string
}
```

- [ ] **Step 4: Add exact API wrappers**

```ts
export const createOutreachDraft = (caseId: number, body: SaveDraftBody) =>
  api.post<CaseCommunication>(`/intervention-cases/${caseId}/outreach/draft`, body).then(r => r.data)
export const updateOutreachDraft = (caseId: number, commId: number, body: SaveDraftBody) =>
  api.put(`/intervention-cases/${caseId}/outreach/draft/${commId}`, body)
export const scheduleAndSendOutreach = (caseId: number, commId: number, body: ScheduleAndSendBody) =>
  api.post(`/intervention-cases/${caseId}/outreach/draft/${commId}/send`, body)
export const retryOutreach = (caseId: number, commId: number) =>
  api.post(`/intervention-cases/${caseId}/outreach/draft/${commId}/retry`)
export const recordMeetingResult = (caseId: number, body: MeetingResultBody) =>
  api.post(`/intervention-cases/${caseId}/outreach/meeting-result`, body)
```

Update `ETAT_LABELS` for the outreach experience: Open → À contacter, InProgress → Email préparé, WaitingStudent → Entretien planifié, Resolved → Entretien réalisé. Keep labels for other states.

- [ ] **Step 5: Run tests and commit**

Run: `npm test -- interventions.test.ts` from `frontend`

Run: `npm run build` from `frontend`

Expected: PASS.

```bash
git add frontend/src/services/interventions.ts frontend/src/services/interventions.test.ts
git commit -m "feat(outreach): add typed frontend outreach API"
```

## Task 8: Turn Cases Into the Four-Stage Intervention Queue

**Files:**
- Modify: `frontend/src/pages/Cases.tsx`
- Create: `frontend/src/pages/Cases.test.tsx`
- Modify: `frontend/src/layout/Sidebar.tsx`

- [ ] **Step 1: Write failing queue tests**

Render with QueryClient and MemoryRouter, mock `fetchCases`, and assert headings and stage placement:

```tsx
expect(await screen.findByRole('heading', { name: 'Interventions' })).toBeInTheDocument()
expect(screen.getByText('À contacter')).toBeInTheDocument()
expect(screen.getByText('Email préparé')).toBeInTheDocument()
expect(screen.getByText('Entretien planifié')).toBeInTheDocument()
expect(screen.getByText('Entretien réalisé')).toBeInTheDocument()
```

Also verify the owner, filière, priority, and stage controls filter locally and clicking a card navigates to `/cases/:id`.

- [ ] **Step 2: Run and verify failure**

Run: `npm test -- Cases.test.tsx` from `frontend`

Expected: FAIL because the page is currently a single list.

- [ ] **Step 3: Implement an accessible four-column queue**

Use this stable mapping:

```ts
const PRIMARY_STAGES = [
  { state: 'Open', label: 'À contacter' },
  { state: 'InProgress', label: 'Email préparé' },
  { state: 'WaitingStudent', label: 'Entretien planifié' },
  { state: 'Resolved', label: 'Entretien réalisé' },
] as const
```

Show Escalated, Monitoring, and Closed records in a separate filtered list so existing cases are never hidden. Each card shows student, priority, owner state, last activity, and meeting/due date. Use buttons with an accessible name containing the student.

- [ ] **Step 4: Rename navigation copy only**

Keep routes at `/cases` and `/cases/:id` to avoid route churn, but change visible sidebar/page text from `Cas` to `Interventions`.

- [ ] **Step 5: Run tests, build, and commit**

Run: `npm test -- Cases.test.tsx` from `frontend`

Run: `npm run build` from `frontend`

Expected: PASS.

```bash
git add frontend/src/pages/Cases.tsx frontend/src/pages/Cases.test.tsx frontend/src/layout/Sidebar.tsx
git commit -m "feat(outreach): add intervention workflow queue"
```

## Task 9: Build the Outreach Composer and Meeting Result UI

**Files:**
- Create: `frontend/src/components/interventions/OutreachComposer.tsx`
- Create: `frontend/src/components/interventions/MeetingOutcomeForm.tsx`
- Modify: `frontend/src/pages/CaseDetail.tsx`
- Create: `frontend/src/pages/CaseDetail.test.tsx`

- [ ] **Step 1: Write failing interaction tests**

Test these paths separately:

```tsx
const baseCase: InterventionCase = {
  id: 12, etudiantId: 4, filiereId: 1, motif: 'Résultats en baisse', priorite: 'High',
  etat: 'InProgress', ownerId: 2, dueDate: null, escaladeLe: null, outcome: null,
  resolutionSummary: null, followUpDate: null, meetingScheduledFor: null,
  meetingLocation: null, meetingAttendance: null, meetingHeldAt: null,
  creeLe: '2026-06-24T09:00:00Z', clotureLe: null,
  etudiant: { id: 4, prenom: 'Sara', nom: 'Amrani' },
}

it('requires review before sending an editable draft', async () => {
  const user = userEvent.setup()
  render(<OutreachComposer intervention={baseCase} communications={[draftCommunication]} canManage onChanged={vi.fn()} />)
  await user.clear(screen.getByLabelText('Sujet'))
  await user.type(screen.getByLabelText('Sujet'), 'Entretien ENIAD')
  await user.type(screen.getByLabelText('Date et heure'), '2026-07-01T10:00')
  await user.type(screen.getByLabelText('Lieu'), 'Salle B12')
  await user.click(screen.getByRole('button', { name: 'Vérifier et envoyer' }))
  expect(screen.getByRole('dialog', { name: 'Confirmer l’envoi' })).toHaveTextContent('Entretien ENIAD')
  expect(scheduleAndSendOutreach).not.toHaveBeenCalled()
  await user.click(screen.getByRole('button', { name: 'Envoyer l’email' }))
  expect(scheduleAndSendOutreach).toHaveBeenCalledWith(12, draftCommunication.id, expect.objectContaining({ location: 'Salle B12' }))
})

it('preserves a failed email and exposes explicit retry', async () => {
  render(<OutreachComposer intervention={baseCase} communications={[{ ...draftCommunication, status: 'Failed', erreur: 'SMTP indisponible' }]} canManage onChanged={vi.fn()} />)
  expect(screen.getByText('SMTP indisponible')).toBeInTheDocument()
  expect(screen.getByRole('button', { name: 'Renvoyer' })).toBeEnabled()
})

it('shows verification state for Queued and blocks retry', async () => {
  render(<OutreachComposer intervention={baseCase} communications={[{ ...draftCommunication, status: 'Queued' }]} canManage onChanged={vi.fn()} />)
  expect(screen.getByText('Vérification de l’envoi en cours')).toBeInTheDocument()
  expect(screen.queryByRole('button', { name: 'Renvoyer' })).not.toBeInTheDocument()
})

it('submits a held meeting outcome', async () => {
  const user = userEvent.setup()
  render(<MeetingOutcomeForm caseId={12} canManage onChanged={vi.fn()} />)
  await user.selectOptions(screen.getByLabelText('Présence'), 'Held')
  await user.type(screen.getByLabelText('Heure réelle'), '2026-06-24T10:00')
  await user.selectOptions(screen.getByLabelText('Résultat'), 'Improved')
  await user.type(screen.getByLabelText('Résumé'), 'L’étudiante a participé.')
  await user.click(screen.getByRole('button', { name: 'Enregistrer l’entretien' }))
  expect(recordMeetingResult).toHaveBeenCalledWith(12, expect.objectContaining({ attendance: 'Held', outcome: 'Improved' }))
})

it('renders outreach actions read-only for Enseignant', async () => {
  render(<OutreachComposer intervention={baseCase} communications={[draftCommunication]} canManage={false} onChanged={vi.fn()} />)
  expect(screen.getByText('Brouillon en attente de validation')).toBeInTheDocument()
  expect(screen.queryByRole('button', { name: 'Envoyer l’email' })).not.toBeInTheDocument()
})
```

At the top of the test file, mock `scheduleAndSendOutreach` and `recordMeetingResult` with `vi.fn().mockResolvedValue({})`, define `draftCommunication` as a complete `CaseCommunication` fixture with status `Draft`, and import `userEvent`, both new components, and the service functions. Wrap renders in the same QueryClient provider used by `Predictions.test.tsx`.

- [ ] **Step 2: Run and verify failure**

Run: `npm test -- CaseDetail.test.tsx` from `frontend`

Expected: FAIL because the components do not exist.

- [ ] **Step 3: Implement `OutreachComposer` as a stage-aware component**

Props must be explicit:

```ts
interface OutreachComposerProps {
  intervention: InterventionCase
  communications: CaseCommunication[]
  canManage: boolean
  onChanged: () => void
}
```

For Open, collect date/location first and call `generateOutreachDraft`, then persist with `createOutreachDraft`. For InProgress, edit subject/body/date/location and show a final confirmation modal containing recipient, complete subject/body, date, time, and location. For Failed, show the stored error and explicit retry. For Queued, show “Vérification de l’envoi en cours” and disable retry. For Sent/WaitingStudent, show the immutable sent copy and meeting details.

- [ ] **Step 4: Implement `MeetingOutcomeForm` and compose the detail page**

Render the outcome form only in `WaitingStudent` and only as editable for Admin/Responsable. Attendance options are Held, Absent, Cancelled. Require held time, outcome, and summary only for Held. After success invalidate `['case', caseId]`, `['case', caseId, 'comms']`, and `['cases']`.

Replace the primary manual transition buttons with the outreach components for Open/InProgress/WaitingStudent/Resolved. Keep generic transition controls in a collapsed “Gestion avancée” section for Escalated, Monitoring, Closed, and reopening.

- [ ] **Step 5: Run tests, build, and commit**

Run: `npm test -- CaseDetail.test.tsx` from `frontend`

Run: `npm run build` from `frontend`

Expected: PASS.

```bash
git add frontend/src/components/interventions/OutreachComposer.tsx frontend/src/components/interventions/MeetingOutcomeForm.tsx frontend/src/pages/CaseDetail.tsx frontend/src/pages/CaseDetail.test.tsx
git commit -m "feat(outreach): add email and meeting workflow UI"
```

## Task 10: Add Funnel Metrics and End-to-End Acceptance

**Files:**
- Modify: `backend/PlateformePFA.API/DTOs/Dashboard/InterventionDashboardDto.cs`
- Modify: `backend/PlateformePFA.API/Controllers/DashboardController.cs`
- Modify: `backend/PlateformePFA.Tests/Controllers/DashboardControllerTests.cs`
- Modify: `frontend/src/services/interventions.ts`
- Modify: `frontend/src/pages/Responsable.tsx`
- Modify: `frontend/e2e/smoke.spec.ts`
- Modify: `README.md`

- [ ] **Step 1: Write failing dashboard metric tests**

Seed one case per primary state plus Sent/Failed communications and assert:

```csharp
dto.NeedsContact.Should().Be(1);
dto.EmailPrepared.Should().Be(1);
dto.MeetingsScheduled.Should().Be(1);
dto.MeetingsHeld.Should().Be(1);
dto.FailedEmails.Should().Be(1);
dto.DuplicateCasesPrevented.Should().BeGreaterThanOrEqualTo(0);
dto.ContactedEligiblePercent.Should().BeInRange(0, 100);
dto.EmailDeliverySuccessPercent.Should().BeInRange(0, 100);
dto.MeetingHeldPercent.Should().BeInRange(0, 100);
dto.MedianHoursRiskToEmail.Should().BeGreaterThanOrEqualTo(0);
```

If duplicate prevention is not persisted today, add an `AuditEntries` count for action `DuplicateCasePrevented` and log that action on the 409 path.

- [ ] **Step 2: Run and verify failure**

Run: `dotnet test backend/PlateformePFA.Tests --filter "FullyQualifiedName~DashboardControllerTests" --nologo`

Expected: FAIL because the funnel properties do not exist.

- [ ] **Step 3: Add funnel fields and Responsable cards**

Extend the DTO with:

```csharp
public int NeedsContact { get; set; }
public int EmailPrepared { get; set; }
public int MeetingsScheduled { get; set; }
public int MeetingsHeld { get; set; }
public int DuplicateCasesPrevented { get; set; }
public decimal ContactedEligiblePercent { get; set; }
public decimal EmailDeliverySuccessPercent { get; set; }
public decimal MeetingHeldPercent { get; set; }
public decimal MedianHoursRiskToEmail { get; set; }
```

Compute counts in SQL/EF by state and audit action. Define eligible students as students with an unresolved high-risk alert; contacted means such a student has a Sent outreach communication. Compute risk-to-email from the earliest linked alert `CreeLe` to the first Sent communication `EnvoyeLe`, using the middle ordered value (or average of the two middle values) as the median. Meeting-held percentage uses scheduled interventions as denominator, and delivery success uses Sent plus Failed as denominator; return zero when a denominator is zero. Add compact funnel cards to `Responsable.tsx`; retain existing operational KPIs.

- [ ] **Step 4: Add the end-to-end scenario**

In Playwright, cover login → Triage → Start intervention → generate/fallback draft → edit → schedule/send → mark Held with outcome → verify Entretien réalisé and timeline. Stub `/api/outreach/draft` and SMTP-dependent backend responses in the browser test so CI is deterministic; backend integration tests remain responsible for real send-state behavior.

- [ ] **Step 5: Run the complete verification matrix**

Run: `dotnet test backend/PlateformePFA.Tests --nologo`

Run: `npm test` from `copilot-runtime`

Run: `npm test -- --run && npm run build && npm run lint` from `frontend`

Run with the stack available: `npm run e2e` from `frontend`

Expected: all commands PASS; Playwright records the complete outreach loop.

- [ ] **Step 6: Update product documentation and commit**

Update the README feature list to describe the action loop and its manual fallback. Do not claim outcome causality.

```bash
git add backend/PlateformePFA.API/DTOs/Dashboard/InterventionDashboardDto.cs backend/PlateformePFA.API/Controllers/DashboardController.cs backend/PlateformePFA.Tests/Controllers/DashboardControllerTests.cs frontend/src/services/interventions.ts frontend/src/pages/Responsable.tsx frontend/e2e/smoke.spec.ts README.md
git commit -m "feat(outreach): measure and verify intervention funnel"
```

## Final Verification

- [ ] Run `git diff --check` and confirm no whitespace errors.
- [ ] Run `dotnet test backend/PlateformePFA.Tests --nologo` and record the passing test count.
- [ ] Run `npm test` in `copilot-runtime` and record the passing test count.
- [ ] Run `npm test -- --run`, `npm run build`, and `npm run lint` in `frontend`.
- [ ] Run `npm run e2e` with the Docker stack available.
- [ ] Manually verify that a Queued/unknown email cannot be retried, a Failed email can be retried, and an absent/cancelled meeting cannot resolve an intervention.
- [ ] Confirm the final diff contains no unrelated workspace changes.
