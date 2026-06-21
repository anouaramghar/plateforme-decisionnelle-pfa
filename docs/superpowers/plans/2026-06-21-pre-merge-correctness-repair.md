# Pre-Merge Correctness Repair Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Repair every merge blocker found in `feature/marouane-decisionnelle` and restore a green backend/frontend verification gate.

**Architecture:** Keep the existing controller/service structure. Add focused DTOs and pure frontend helpers, use explicit active-student predicates in operational queries, and route all new note entry through one natural-key upsert endpoint. Validate bulk imports completely before one database save so failures are atomic.

**Tech Stack:** ASP.NET Core 8, EF Core 8, xUnit, FluentAssertions, React 18, TypeScript 5, TanStack Query, SheetJS (`xlsx`), Vitest, Vite.

---

## File map

- `backend/PlateformePFA.API/DTOs/Notes/NoteDto.cs`: note-upsert request/response contracts.
- `backend/PlateformePFA.API/DTOs/Etudiants/EtudiantDto.cs`: bulk-import request/error/result contracts.
- `backend/PlateformePFA.API/Controllers/NotesController.cs`: active-student note upsert.
- `backend/PlateformePFA.API/Controllers/EtudiantsController.cs`: active detail/update/delete behavior and atomic import.
- `backend/PlateformePFA.API/Controllers/DashboardController.cs`: active operational projections and as-of population math.
- `backend/PlateformePFA.API/Services/AlerteService.cs`: active-only scans and creation counting.
- `backend/PlateformePFA.API/Services/ReportGenerator.cs`: active-only operational report cohort.
- `backend/PlateformePFA.API/Controllers/PredictionsController.cs`: active-only prediction cohorts/lookups.
- `backend/PlateformePFA.Tests/Controllers/NotesControllerTests.cs`: note-upsert regression tests.
- `backend/PlateformePFA.Tests/Controllers/EtudiantsControllerTests.cs`: withdrawal and atomic-import tests.
- `backend/PlateformePFA.Tests/Controllers/DashboardControllerTests.cs`: as-of count regression tests.
- `backend/PlateformePFA.Tests/Services/AlerteServiceTests.cs`: active scan and created-count tests.
- `backend/PlateformePFA.Tests/Controllers/CopilotControllerTests.cs`: remove obsolete test suite whose production API no longer exists.
- `frontend/src/services/notes.ts`: module query and note-upsert client.
- `frontend/src/services/studentImport.ts`: pure workbook normalization and import API client.
- `frontend/src/auth/roles.ts`: pure role-policy helpers.
- `frontend/src/components/auth/RoleRoute.tsx`: route authorization UI.
- `frontend/src/pages/Students.tsx`, `StudentProfile.tsx`, `Enseignant.tsx`: use real modules, note upsert, atomic import, and role-aware actions.
- `frontend/src/pages/Admin.tsx`, `frontend/src/App.tsx`: Responsable student-management access and guarded routes.
- `frontend/src/services/studentImport.test.ts`, `frontend/src/auth/roles.test.ts`: frontend regressions.
- `frontend/package.json`, `frontend/package-lock.json`, `frontend/vite.config.ts`: compatible chart versions and Vitest.

### Task 1: Restore executable verification baselines

**Files:**
- Modify: `frontend/package.json`
- Modify: `frontend/package-lock.json`
- Modify: `frontend/vite.config.ts`
- Delete: `backend/PlateformePFA.Tests/Controllers/CopilotControllerTests.cs`

- [ ] **Step 1: Prove the current baseline failures**

Run:

```powershell
dotnet test backend/PlateformePFA.Tests/PlateformePFA.Tests.csproj --configuration Release --nologo
npm ci --prefix frontend --dry-run
```

Expected: backend compilation fails on missing `DTOs.Copilot`, `IAgentServiceClient`, and `AgentRunRequest`; npm fails because locked `react-apexcharts@1.9.0` requires ApexCharts 4+.

- [ ] **Step 2: Remove only the obsolete Copilot test file**

Delete `CopilotControllerTests.cs`; retain `CopilotToolControllerTests.cs`, which covers the production controller that still exists.

- [ ] **Step 3: Pin compatible frontend dependencies and add Vitest**

Update scripts and versions:

```json
{
  "scripts": {
    "test": "vitest run --passWithNoTests",
    "build": "tsc -b && vite build",
    "lint": "eslint src --ext ts,tsx --report-unused-disable-directives --max-warnings 0"
  },
  "dependencies": {
    "apexcharts": "3.54.1",
    "react-apexcharts": "1.4.1"
  },
  "devDependencies": {
    "vitest": "^2.1.9"
  }
}
```

Regenerate with:

```powershell
npm install --prefix frontend
```

- [ ] **Step 4: Configure Vitest**

Add to `vite.config.ts`:

```ts
/// <reference types="vitest" />
export default defineConfig({
  plugins: [react()],
  test: { environment: 'node' },
})
```

- [ ] **Step 5: Verify the restored baselines**

Run:

```powershell
dotnet test backend/PlateformePFA.Tests/PlateformePFA.Tests.csproj --configuration Release --nologo
npm ci --prefix frontend
npm run test --prefix frontend
```

Expected: backend tests compile and run; npm installs without peer errors; Vitest exits successfully with no tests or with the tests added in later tasks.

- [ ] **Step 6: Commit**

```powershell
git add backend/PlateformePFA.Tests/Controllers/CopilotControllerTests.cs frontend/package.json frontend/package-lock.json frontend/vite.config.ts
git commit -m "chore: restore backend and frontend verification"
```

### Task 2: Add natural-key note upsert

**Files:**
- Create: `backend/PlateformePFA.Tests/Controllers/NotesControllerTests.cs`
- Modify: `backend/PlateformePFA.API/DTOs/Notes/NoteDto.cs`
- Modify: `backend/PlateformePFA.API/Controllers/NotesController.cs`

- [ ] **Step 1: Write failing create-and-update tests**

Add tests that authenticate as Admin, use `SampleData.SeedOne`, and call `PUT /api/notes/upsert` twice:

```csharp
[Fact]
public async Task Upsert_creates_new_natural_key()
{
    var payload = new { etudiantId, moduleId, noteFinal = 9m, annee = "2026/2027", semestre = "S1" };
    var response = await client.PutAsJsonAsync("/api/notes/upsert", payload);
    response.StatusCode.Should().Be(HttpStatusCode.Created);
    using var db = _factory.CreateContext();
    db.Notes.Count(n => n.EtudiantId == etudiantId && n.ModuleId == moduleId && n.Annee == "2026/2027" && n.Semestre == "S1").Should().Be(1);
}

[Fact]
public async Task Upsert_updates_matching_natural_key_without_duplicate()
{
    var payload = new { etudiantId, moduleId, noteFinal = 15m, annee = "2025/2026", semestre = "S2" };
    var response = await client.PutAsJsonAsync("/api/notes/upsert", payload);
    response.StatusCode.Should().Be(HttpStatusCode.OK);
    using var db = _factory.CreateContext();
    db.Notes.Count(n => n.EtudiantId == etudiantId && n.ModuleId == moduleId && n.Annee == "2025/2026" && n.Semestre == "S2").Should().Be(1);
    db.Notes.Single(n => n.EtudiantId == etudiantId && n.ModuleId == moduleId && n.Annee == "2025/2026" && n.Semestre == "S2").NoteFinal.Should().Be(15m);
}
```

Add a third test expecting `404 NotFound` for a withdrawn student.

- [ ] **Step 2: Run the tests and verify RED**

```powershell
dotnet test backend/PlateformePFA.Tests/PlateformePFA.Tests.csproj --filter NotesControllerTests --nologo
```

Expected: all new tests fail with 404 because `/api/notes/upsert` does not exist.

- [ ] **Step 3: Add contracts**

```csharp
public class UpsertNoteDto : CreateNoteDto { }

public class UpsertNoteResultDto
{
    public int Id { get; set; }
    public bool Created { get; set; }
}
```

- [ ] **Step 4: Implement the endpoint**

Add an Admin/Enseignant `PUT` action that:

```csharp
var studentExists = await _context.Etudiants.AnyAsync(e => e.Id == dto.EtudiantId && e.DesinscritLe == null);
if (!studentExists) return NotFound(new { message = "Etudiant actif introuvable." });
if (!await _context.Modules.AnyAsync(m => m.Id == dto.ModuleId))
    return NotFound(new { message = "Module introuvable." });

var note = await _context.Notes.SingleOrDefaultAsync(n =>
    n.EtudiantId == dto.EtudiantId && n.ModuleId == dto.ModuleId &&
    n.Annee == dto.Annee && n.Semestre == dto.Semestre);
var created = note == null;
note ??= new Note { EtudiantId = dto.EtudiantId, ModuleId = dto.ModuleId, Annee = dto.Annee, Semestre = dto.Semestre, CreeLe = DateTime.UtcNow };
note.NoteTD = dto.NoteTD;
note.NoteTP = dto.NoteTP;
note.NoteExamen = dto.NoteExamen;
note.NoteFinal = dto.NoteFinal;
if (created) _context.Notes.Add(note);
await _context.SaveChangesAsync();
await _alerteService.CheckNoteAlertAsync(note.EtudiantId, note.ModuleId);
var result = new UpsertNoteResultDto { Id = note.Id, Created = created };
return created ? StatusCode(StatusCodes.Status201Created, result) : Ok(result);
```

- [ ] **Step 5: Verify GREEN**

Run the filtered tests and then the whole backend suite. Expected: all note tests and existing tests pass.

- [ ] **Step 6: Commit**

```powershell
git add backend/PlateformePFA.API/DTOs/Notes/NoteDto.cs backend/PlateformePFA.API/Controllers/NotesController.cs backend/PlateformePFA.Tests/Controllers/NotesControllerTests.cs
git commit -m "fix: upsert notes by academic key"
```

### Task 3: Make withdrawal semantics operationally consistent

**Files:**
- Modify: `backend/PlateformePFA.Tests/Controllers/EtudiantsControllerTests.cs`
- Modify: `backend/PlateformePFA.Tests/Controllers/DashboardControllerTests.cs`
- Modify: `backend/PlateformePFA.API/Controllers/EtudiantsController.cs`
- Modify: `backend/PlateformePFA.API/Controllers/DashboardController.cs`
- Modify: `backend/PlateformePFA.API/Controllers/PredictionsController.cs`
- Modify: `backend/PlateformePFA.API/Services/ReportGenerator.cs`

- [ ] **Step 1: Write failing withdrawal tests**

Add tests proving:

```csharp
// A withdrawn student returns 404 from GET /api/etudiants/{id},
// GET /api/etudiants/{id}/notes, and PUT /api/etudiants/{id}.
// Repeating DELETE preserves the first DesinscritLe value.
// The dashboard excludes withdrawn alerts and absences.
```

For idempotency, capture `firstWithdrawal`, call DELETE again, and assert equality after reloading.

- [ ] **Step 2: Write failing as-of population test**

Seed one old active student, one old student withdrawn yesterday, and one student created yesterday. Assert current count is two, count 30 days ago is two, delta is `0m`, and historical sparkline contains the withdrawn student before its withdrawal date.

- [ ] **Step 3: Run filtered tests and verify RED**

```powershell
dotnet test backend/PlateformePFA.Tests/PlateformePFA.Tests.csproj --filter "EtudiantsControllerTests|DashboardControllerTests" --nologo
```

Expected: detail/update/idempotency and as-of assertions fail against current behavior.

- [ ] **Step 4: Implement active endpoint checks**

Use `e.DesinscritLe == null` in student detail, note detail, and update lookups. Make deletion idempotent:

```csharp
if (etudiant.DesinscritLe == null)
{
    etudiant.DesinscritLe = DateTime.UtcNow;
    await _context.SaveChangesAsync();
}
return NoContent();
```

- [ ] **Step 5: Implement as-of dashboard math**

For cutoff `at`, active population is:

```csharp
e.CreeLe < at && (e.DesinscritLe == null || e.DesinscritLe >= at)
```

Use it for the 30-day denominator and each of the 14 sparkline cutoffs. Add `a.Etudiant.DesinscritLe == null` and `a.Etudiant.DesinscritLe == null` equivalents to current alert/absence operational queries.

- [ ] **Step 6: Filter prediction and report cohorts**

Add `DesinscritLe == null` to every operational `_context.Etudiants` root in `PredictionsController` and `ReportGenerator.BuildDatasetAsync`. Direct prediction/explanation lookups return 404 for withdrawn IDs.

- [ ] **Step 7: Verify GREEN and commit**

Run the filtered tests, full backend suite, then commit:

```powershell
git add backend/PlateformePFA.API backend/PlateformePFA.Tests/Controllers/EtudiantsControllerTests.cs backend/PlateformePFA.Tests/Controllers/DashboardControllerTests.cs
git commit -m "fix: exclude withdrawn students from operations"
```

### Task 4: Make alert scans active-only with exact creation counts

**Files:**
- Create: `backend/PlateformePFA.Tests/Services/AlerteServiceTests.cs`
- Modify: `backend/PlateformePFA.API/Services/AlerteService.cs`

- [ ] **Step 1: Write failing service tests**

Create a scoped `AlerteService` with the in-memory context. Test that a withdrawn low-grade student produces zero new alerts and that resolving one old alert while creating another reports `notes == 1`, never `0` or `-1`.

- [ ] **Step 2: Verify RED**

```powershell
dotnet test backend/PlateformePFA.Tests/PlateformePFA.Tests.csproj --filter AlerteServiceTests --nologo
```

Expected: withdrawn scan creates an alert and the before/after count assertion fails.

- [ ] **Step 3: Implement active scan and inserted-ID counting**

Capture existing alert IDs before scanning:

```csharp
var beforeIds = await _context.Alertes.AsNoTracking().Select(a => a.Id).ToHashSetAsync();
```

Build student IDs and note pairs with `Etudiant.DesinscritLe == null`, perform checks, then count records not in `beforeIds` grouped by type. This counts creations independently of resolutions.

- [ ] **Step 4: Verify GREEN and commit**

Run service tests plus the full suite and commit `AlerteService.cs` with its tests.

### Task 5: Add atomic bulk student import

**Files:**
- Modify: `backend/PlateformePFA.API/DTOs/Etudiants/EtudiantDto.cs`
- Modify: `backend/PlateformePFA.API/Controllers/EtudiantsController.cs`
- Modify: `backend/PlateformePFA.Tests/Controllers/EtudiantsControllerTests.cs`

- [ ] **Step 1: Write failing import tests**

Test a valid two-row payload returns 200 and creates both. Test a payload containing one valid row plus one duplicate/invalid row returns 400 and creates neither. Assert errors contain the one-based source row.

- [ ] **Step 2: Verify RED**

Call `POST /api/etudiants/import`; expect 404 for both new tests.

- [ ] **Step 3: Add import DTOs**

```csharp
public class ImportEtudiantRowDto
{
    public int RowNumber { get; set; }
    public string Matricule { get; set; } = string.Empty;
    public string Nom { get; set; } = string.Empty;
    public string Prenom { get; set; } = string.Empty;
    public string? Email { get; set; }
    public string FiliereCode { get; set; } = string.Empty;
    public string Niveau { get; set; } = string.Empty;
    public string Annee { get; set; } = string.Empty;
}
public record ImportEtudiantErrorDto(int RowNumber, string Field, string Message);
public record ImportEtudiantsResultDto(int Imported, IReadOnlyList<ImportEtudiantErrorDto> Errors);
```

- [ ] **Step 4: Validate completely before mutation**

Load filieres and existing matricules once. Validate required strings, `Validation.AnneePattern`, the fixed level set, within-file duplicates, stored duplicates, and filiere codes. Return `BadRequest(new ImportEtudiantsResultDto(0, errors))` if any error exists.

- [ ] **Step 5: Save atomically**

Map every validated row, `AddRange`, then call `SaveChangesAsync` exactly once. Return `Ok(new ImportEtudiantsResultDto(entities.Count, Array.Empty<ImportEtudiantErrorDto>()))`.

- [ ] **Step 6: Verify GREEN and commit**

Run import tests and full suite; commit controller, DTO, and tests.

### Task 6: Add tested frontend import and role helpers

**Files:**
- Create: `frontend/src/services/studentImport.ts`
- Create: `frontend/src/services/studentImport.test.ts`
- Create: `frontend/src/auth/roles.ts`
- Create: `frontend/src/auth/roles.test.ts`

- [ ] **Step 1: Write failing import parser tests**

```ts
it('normalizes BOM headers and quoted comma values', () => {
  const csv = '\uFEFFmatricule,nom,prenom,filiere,niveau,annee\nE1,"Doe, Jr",Jane,GI,CI1,2025/2026'
  expect(parseStudentCsv(csv)).toEqual([{ rowNumber: 2, matricule: 'E1', nom: 'Doe, Jr', prenom: 'Jane', filiereCode: 'GI', niveau: 'CI1', annee: '2025/2026', email: '' }])
})

it('parses semicolon-delimited CSV', () => {
  const csv = 'matricule;nom;prenom;filiere;niveau;annee\nE2;Ali;Sara;IA;CI2;2025/2026'
  expect(parseStudentCsv(csv)[0].filiereCode).toBe('IA')
})
```

- [ ] **Step 2: Write failing role-policy tests**

```ts
expect(canManageStudents('Responsable')).toBe(true)
expect(canDeleteStudents('Responsable')).toBe(false)
expect(canAccessRolePage('Enseignant', 'enseignant')).toBe(true)
expect(canAccessRolePage('Responsable', 'enseignant')).toBe(false)
```

- [ ] **Step 3: Verify RED**

```powershell
npm run test --prefix frontend
```

Expected: modules/functions cannot be resolved.

- [ ] **Step 4: Implement pure helpers**

Use `XLSX.read(csv, { type: 'string' })`, select the first sheet, call `sheet_to_json<Record<string, unknown>>({ defval: '' })`, normalize header keys by trimming BOM/whitespace and lowercasing, and map aliases `filiere|filierecode|filière` to `filiereCode`.

Implement role helpers with explicit allowlists rather than string ordering.

- [ ] **Step 5: Verify GREEN and commit**

Run Vitest and commit the four helper/test files.

### Task 7: Wire note upsert and real module catalogues

**Files:**
- Create: `frontend/src/services/notes.ts`
- Modify: `frontend/src/pages/Students.tsx`
- Modify: `frontend/src/pages/StudentProfile.tsx`
- Modify: `frontend/src/pages/Enseignant.tsx`

- [ ] **Step 1: Add the shared API client**

```ts
export interface ModuleOption { id: number; code: string; nom: string }
export async function fetchModules(): Promise<ModuleOption[]> {
  const response = await api.get<{ items: ModuleOption[] }>('/modules?pageSize=100')
  return response.data.items
}
export async function upsertNote(payload: NoteUpsertPayload) {
  return (await api.put<NoteUpsertResult>('/notes/upsert', payload)).data
}
```

- [ ] **Step 2: Replace module options in all three note forms**

Use query key `['modules']` and render options from `fetchModules`, filtered by the active student's filiere/niveau when those fields exist in the module response. Never derive selectable modules from `notes`.

- [ ] **Step 3: Replace POST with upsert and add visible errors**

Each submit handler calls `upsertNote`, catches Axios error messages into local `noteError`, leaves form values intact, and only closes/resets on success.

- [ ] **Step 4: Verify and commit**

Run frontend tests, TypeScript build, and ESLint. Commit the API client and three pages.

### Task 8: Wire atomic CSV import and role-aware UI

**Files:**
- Create: `frontend/src/components/auth/RoleRoute.tsx`
- Modify: `frontend/src/pages/Students.tsx`
- Modify: `frontend/src/pages/Admin.tsx`
- Modify: `frontend/src/App.tsx`

- [ ] **Step 1: Replace sequential CSV POSTs**

Read the file as text, call `parseStudentCsv`, then make one request:

```ts
const rows = parseStudentCsv(await file.text())
const result = await api.post<ImportResult>('/etudiants/import', rows)
```

Display `Imported: N` on success. On HTTP 400, render every returned `rowNumber`, `field`, and `message`; do not refetch unless the request succeeds.

- [ ] **Step 2: Add RoleRoute**

```tsx
export function RoleRoute({ roles, children }: { roles: string[]; children: React.ReactNode }) {
  const { user } = useAuth()
  return user && roles.includes(user.role)
    ? <>{children}</>
    : <Navigate to="/dashboard" replace />
}
```

- [ ] **Step 3: Guard role-specific routes**

Wrap `/enseignant` with roles `['Admin', 'Enseignant']`, `/responsable` with `['Admin', 'Responsable']`, and `/admin` with `['Admin', 'Responsable']`.

- [ ] **Step 4: Restrict Responsable inside Admin**

For Responsable, force the selected tab to `etudiants`, render only that tab, and preserve existing `isAdmin` checks so delete remains hidden. Users and DW tabs remain Admin-only.

- [ ] **Step 5: Gate actions on Students**

Use role helpers so CSV import and enrollment show only for Admin/Responsable, note entry only for Admin/Enseignant, and manual alert creation only for Admin.

- [ ] **Step 6: Verify and commit**

Run Vitest, build, and lint; commit the route component and modified pages.

### Task 9: Final merge gate

**Files:**
- Verify all modified files.

- [ ] **Step 1: Run backend verification**

```powershell
dotnet test backend/PlateformePFA.Tests/PlateformePFA.Tests.csproj --configuration Release --nologo
```

Expected: build succeeds and every test passes with zero failures.

- [ ] **Step 2: Run frontend verification from a clean install**

```powershell
npm ci --prefix frontend
npm run test --prefix frontend
npm run build --prefix frontend
npm run lint --prefix frontend
```

Expected: installation, tests, TypeScript/Vite build, and ESLint all exit 0.

- [ ] **Step 3: Run repository checks**

```powershell
docker compose config --quiet
git diff --check origin/main...HEAD
git status --short --branch
```

Expected: Compose and whitespace checks exit 0; status shows only intentional plan tracking if it has not yet been committed.

- [ ] **Step 4: Review the full branch diff**

```powershell
git diff --stat origin/main...HEAD
git diff --name-status origin/main...HEAD
```

Confirm no generated `dist`, `bin`, `obj`, coverage, `.env`, or `node_modules` content is tracked.

- [ ] **Step 5: Commit final plan tracking if needed**

```powershell
git add docs/superpowers/plans/2026-06-21-pre-merge-correctness-repair.md
git commit -m "docs: add pre-merge repair implementation plan"
```
