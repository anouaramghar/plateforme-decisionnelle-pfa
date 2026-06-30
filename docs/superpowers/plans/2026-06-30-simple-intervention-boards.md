# Simple Intervention Boards Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Simplify interventions by giving teachers a lightweight follow-up board and admins a next-action intervention board.

**Architecture:** Reuse existing intervention cases, notes, communications, and role routes. Add one narrow teacher follow-up API that derives board columns from existing case data and teacher-authored note markers; no new table or workflow engine. Update the current frontend pages instead of adding a parallel intervention product.

**Tech Stack:** ASP.NET Core 8, EF Core, React 18, TypeScript, TanStack Query, Vitest, xUnit.

---

## File Structure

- Modify `backend/PlateformePFA.API/Controllers/EnseignantController.cs` to expose teacher follow-up cards and teacher actions.
- Create `backend/PlateformePFA.API/DTOs/Enseignant/TeacherFollowUpDtos.cs` for the small API contract.
- Create `backend/PlateformePFA.Tests/Controllers/EnseignantFollowUpControllerTests.cs` to cover the new endpoints.
- Modify `frontend/src/services/interventions.ts` to add teacher follow-up types and calls.
- Modify `frontend/src/pages/Enseignant.tsx` to add a "Suivi etudiants" board above the existing grade/absence workspace.
- Create `frontend/src/pages/Enseignant.test.tsx` for the teacher board.
- Modify `frontend/src/pages/Cases.tsx` to rename/re-map the admin board columns by next action.
- Modify `frontend/src/pages/CaseDetail.tsx` to make the primary next action obvious and keep advanced/admin machinery collapsed.
- Modify `frontend/src/pages/Cases.test.tsx` and `frontend/src/pages/CaseDetail.test.tsx` for the new admin labels and behavior.

## Task 1: Teacher Follow-Up API

**Files:**
- Create: `backend/PlateformePFA.API/DTOs/Enseignant/TeacherFollowUpDtos.cs`
- Modify: `backend/PlateformePFA.API/Controllers/EnseignantController.cs`
- Test: `backend/PlateformePFA.Tests/Controllers/EnseignantFollowUpControllerTests.cs`

- [ ] **Step 1: Write failing backend tests**

Create `backend/PlateformePFA.Tests/Controllers/EnseignantFollowUpControllerTests.cs` with tests shaped like existing controller tests:

```csharp
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Threading;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using PlateformePFA.API.Data;
using PlateformePFA.API.Models;
using PlateformePFA.API.Services;
using PlateformePFA.Tests.Fixtures;

namespace PlateformePFA.Tests.Controllers;

public class EnseignantFollowUpControllerTests : IClassFixture<TestWebFactory>
{
    private readonly TestWebFactory _factory;
    public EnseignantFollowUpControllerTests(TestWebFactory factory)
    {
        _factory = factory;
        _factory.SeedAdmin();
        using var ctx = _factory.CreateContext();
        SampleData.SeedOne(ctx);
    }

    [Fact]
    public async Task Follow_up_board_returns_teacher_cards()
    {
        using var db = _factory.CreateContext();
        await SeedTeacherCaseAsync(db);

        var client = await TeacherClientAsync();
        var cards = await client.GetFromJsonAsync<List<TeacherCard>>("/api/enseignant/suivi");

        cards.Should().NotBeNull();
        cards!.Should().Contain(c => c.StudentName.Contains("Sara") && c.Column == "A voir");
    }

    [Fact]
    public async Task Add_observation_creates_public_case_note()
    {
        using var db = _factory.CreateContext();
        var caseId = await SeedTeacherCaseAsync(db);

        var client = await TeacherClientAsync();
        var res = await client.PostAsJsonAsync($"/api/enseignant/suivi/{caseId}/observation", new
        {
            contenu = "L'etudiant a rendu le TP apres relance."
        });

        res.StatusCode.Should().Be(HttpStatusCode.Created);
        db.CaseNotes.Any(n => n.CaseId == caseId && !n.IsPrivate && n.Contenu.Contains("TP")).Should().BeTrue();
    }

    [Fact]
    public async Task Mark_treated_adds_teacher_marker_note()
    {
        using var db = _factory.CreateContext();
        var caseId = await SeedTeacherCaseAsync(db);

        var client = await TeacherClientAsync();
        var res = await client.PostAsJsonAsync($"/api/enseignant/suivi/{caseId}/treated", new
        {
            contenu = "Situation reglee cote module."
        });

        res.StatusCode.Should().Be(HttpStatusCode.Created);
        db.CaseNotes.Any(n => n.CaseId == caseId && n.Contenu.StartsWith("[teacher-follow-up:treated]")).Should().BeTrue();
    }

    private async Task<HttpClient> TeacherClientAsync()
    {
        using (var ctx = _factory.CreateContext())
        {
            var module = ctx.Modules.Single(m => m.Code == "GI01");
            if (!ctx.Utilisateurs.Any(u => u.Email == "teacher-followup@eniad.ma"))
            {
                ctx.Utilisateurs.Add(new Utilisateur
                {
                    Email = "teacher-followup@eniad.ma",
                    Nom = "Teach",
                    Prenom = "Er",
                    MotDePasseHash = BCrypt.Net.BCrypt.HashPassword("TeacherPass!2026"),
                    Role = "Enseignant",
                    EstActif = true,
                    ModuleId = module.Id,
                });
                ctx.SaveChanges();
            }
        }
        var client = _factory.CreateClient();
        var token = await AuthHelper.GetTokenAsync(client, "teacher-followup@eniad.ma", "TeacherPass!2026");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    private static int _seq;
    private static async Task<int> SeedTeacherCaseAsync(AppDbContext db)
    {
        var filiere = await db.Filieres.SingleAsync(f => f.Code == "GI");
        var seq = Interlocked.Increment(ref _seq);
        var student = new Etudiant { Nom = "Amrani", Prenom = "Sara", Matricule = $"TFU{seq:D5}", FiliereId = filiere.Id, Niveau = "CI1", Annee = "2025/2026" };
        db.Etudiants.Add(student);
        await db.SaveChangesAsync();

        var c = new InterventionCase
        {
            EtudiantId = student.Id,
            FiliereId = filiere.Id,
            Motif = "Risque sur le module",
            Priorite = "High",
            Etat = CaseWorkflowState.Open,
            CreeLe = DateTime.UtcNow
        };
        db.InterventionCases.Add(c);
        await db.SaveChangesAsync();

        return c.Id;
    }

    private sealed record TeacherCard(int CaseId, string StudentName, string Motif, string Priority, string Column);
}
```

- [ ] **Step 2: Run test to verify it fails**

Run:

```powershell
cd backend/PlateformePFA.Tests
dotnet test --nologo --filter EnseignantFollowUpControllerTests
```

Expected: FAIL because `/api/enseignant/suivi` and action endpoints do not exist.

- [ ] **Step 3: Add DTOs**

Create `backend/PlateformePFA.API/DTOs/Enseignant/TeacherFollowUpDtos.cs`:

```csharp
namespace PlateformePFA.API.DTOs.Enseignant;

public sealed record TeacherFollowUpCardDto(
    int CaseId,
    int EtudiantId,
    string StudentName,
    string Motif,
    string Priority,
    string Column,
    string? LastAction,
    DateTime CreeLe);

public sealed record TeacherFollowUpActionDto(string Contenu);
```

- [ ] **Step 4: Implement the minimal API**

In `backend/PlateformePFA.API/Controllers/EnseignantController.cs`, add imports:

```csharp
using PlateformePFA.API.DTOs.Enseignant;
using PlateformePFA.API.Models;
using PlateformePFA.API.Services;
```

Add these constants and methods inside `EnseignantController`:

```csharp
private const string TreatedMarker = "[teacher-follow-up:treated]";
private const string RequestedMarker = "[teacher-follow-up:requested]";

private async Task<bool> CanAccessCaseAsync(int caseId)
{
    var userId = CurrentUserId;
    if (userId is null) return false;

    var utilisateur = await _context.Utilisateurs
        .AsNoTracking()
        .Include(u => u.Module)
        .FirstOrDefaultAsync(u => u.Id == userId);

    if (utilisateur?.Module is null) return false;

    return await _context.InterventionCases
        .Include(c => c.Etudiant)
        .AnyAsync(c => c.Id == caseId
            && c.Etudiant.FiliereId == utilisateur.Module.FiliereId
            && c.Etudiant.Niveau == utilisateur.Module.Niveau);
}

[HttpGet("suivi")]
public async Task<ActionResult<IEnumerable<TeacherFollowUpCardDto>>> GetSuivi()
{
    var userId = CurrentUserId;
    if (userId is null) return Unauthorized();

    var utilisateur = await _context.Utilisateurs
        .AsNoTracking()
        .Include(u => u.Module)
        .FirstOrDefaultAsync(u => u.Id == userId);

    if (utilisateur?.Module is null) return Ok(Array.Empty<TeacherFollowUpCardDto>());

    var cases = await _context.InterventionCases
        .AsNoTracking()
        .Include(c => c.Etudiant)
        .Where(c => c.Etudiant.FiliereId == utilisateur.Module.FiliereId
            && c.Etudiant.Niveau == utilisateur.Module.Niveau)
        .OrderByDescending(c => c.CreeLe)
        .Take(100)
        .ToListAsync();

    var caseIds = cases.Select(c => c.Id).ToArray();
    var notes = await _context.CaseNotes
        .AsNoTracking()
        .Where(n => caseIds.Contains(n.CaseId) && !n.IsPrivate)
        .OrderByDescending(n => n.CreeLe)
        .ToListAsync();

    var byCase = notes.GroupBy(n => n.CaseId).ToDictionary(g => g.Key, g => g.ToList());

    return cases.Select(c =>
    {
        byCase.TryGetValue(c.Id, out var caseNotes);
        var hasTeacherNote = caseNotes?.Any(n => n.AuteurId == userId) == true;
        var treated = caseNotes?.Any(n => n.AuteurId == userId && n.Contenu.StartsWith(TreatedMarker)) == true
            || c.Etat == CaseWorkflowState.Resolved
            || c.Etat == CaseWorkflowState.Closed;
        var column = treated ? "Traite" : hasTeacherNote ? "En suivi" : "A voir";

        return new TeacherFollowUpCardDto(
            c.Id,
            c.EtudiantId,
            $"{c.Etudiant.Prenom} {c.Etudiant.Nom}",
            c.Motif,
            c.Priorite,
            column,
            caseNotes?.FirstOrDefault()?.Contenu.Replace(TreatedMarker, "").Trim(),
            c.CreeLe);
    }).ToList();
}

[HttpPost("suivi/{caseId:int}/observation")]
public async Task<ActionResult<CaseNote>> AddObservation(int caseId, TeacherFollowUpActionDto dto)
{
    if (string.IsNullOrWhiteSpace(dto.Contenu)) return BadRequest(new { message = "Observation requise." });
    if (!await CanAccessCaseAsync(caseId)) return NotFound(new { message = "Cas introuvable." });

    var userId = CurrentUserId;
    var name = User.FindFirstValue("NomComplet") ?? User.FindFirstValue(ClaimTypes.Email) ?? "Enseignant";
    var note = new CaseNote
    {
        CaseId = caseId,
        Contenu = dto.Contenu.Trim(),
        IsPrivate = false,
        AuteurId = userId,
        AuteurNom = name,
    };

    _context.CaseNotes.Add(note);
    await _context.SaveChangesAsync();
    return Created($"/api/intervention-cases/{caseId}/notes", note);
}

[HttpPost("suivi/{caseId:int}/treated")]
public async Task<ActionResult<CaseNote>> MarkTreated(int caseId, TeacherFollowUpActionDto dto)
{
    if (!await CanAccessCaseAsync(caseId)) return NotFound(new { message = "Cas introuvable." });

    var text = string.IsNullOrWhiteSpace(dto.Contenu) ? "Marque traite par l'enseignant." : dto.Contenu.Trim();
    var userId = CurrentUserId;
    var name = User.FindFirstValue("NomComplet") ?? User.FindFirstValue(ClaimTypes.Email) ?? "Enseignant";
    var note = new CaseNote
    {
        CaseId = caseId,
        Contenu = $"{TreatedMarker} {text}",
        IsPrivate = false,
        AuteurId = userId,
        AuteurNom = name,
    };

    _context.CaseNotes.Add(note);
    await _context.SaveChangesAsync();
    return Created($"/api/intervention-cases/{caseId}/notes", note);
}

[HttpPost("suivi/{caseId:int}/request-intervention")]
public async Task<ActionResult<CaseNote>> RequestIntervention(int caseId, TeacherFollowUpActionDto dto)
{
    if (!await CanAccessCaseAsync(caseId)) return NotFound(new { message = "Cas introuvable." });

    var text = string.IsNullOrWhiteSpace(dto.Contenu) ? "Intervention demandee par l'enseignant." : dto.Contenu.Trim();
    var userId = CurrentUserId;
    var name = User.FindFirstValue("NomComplet") ?? User.FindFirstValue(ClaimTypes.Email) ?? "Enseignant";
    var note = new CaseNote
    {
        CaseId = caseId,
        Contenu = $"{RequestedMarker} {text}",
        IsPrivate = false,
        AuteurId = userId,
        AuteurNom = name,
    };

    _context.CaseNotes.Add(note);
    await _context.SaveChangesAsync();
    return Created($"/api/intervention-cases/{caseId}/notes", note);
}
```

- [ ] **Step 5: Run backend tests**

Run:

```powershell
cd backend/PlateformePFA.Tests
dotnet test --nologo --filter EnseignantFollowUpControllerTests
```

Expected: PASS.

- [ ] **Step 6: Commit**

```powershell
git add backend/PlateformePFA.API/DTOs/Enseignant/TeacherFollowUpDtos.cs backend/PlateformePFA.API/Controllers/EnseignantController.cs backend/PlateformePFA.Tests/Controllers/EnseignantFollowUpControllerTests.cs
git commit -m "feat(interventions): add teacher follow-up API"
```

## Task 2: Teacher Follow-Up Board

**Files:**
- Modify: `frontend/src/services/interventions.ts`
- Modify: `frontend/src/pages/Enseignant.tsx`
- Test: `frontend/src/pages/Enseignant.test.tsx`

- [ ] **Step 1: Write failing frontend test**

Create `frontend/src/pages/Enseignant.test.tsx`:

```tsx
import { render, screen, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { MemoryRouter } from 'react-router-dom'
import { vi, describe, it, expect, beforeEach } from 'vitest'
import Enseignant from './Enseignant'

vi.mock('../context/AuthContext', () => ({
  useAuth: () => ({ user: { nom: 'Prof', role: 'Enseignant' } }),
}))

const getMock = vi.fn()
const postMock = vi.fn()
vi.mock('../services/api', () => ({
  api: {
    get: (...args: unknown[]) => getMock(...args),
    post: (...args: unknown[]) => postMock(...args),
  },
}))

function renderPage() {
  const queryClient = new QueryClient({ defaultOptions: { queries: { retry: false } } })
  render(
    <QueryClientProvider client={queryClient}>
      <MemoryRouter>
        <Enseignant />
      </MemoryRouter>
    </QueryClientProvider>,
  )
}

describe('Enseignant follow-up board', () => {
  beforeEach(() => {
    vi.clearAllMocks()
    postMock.mockResolvedValue({ data: {} })
    getMock.mockImplementation((url: string) => {
      if (url === '/enseignant/mon-module') return Promise.resolve({ data: { moduleId: 1, moduleCode: 'MATH', moduleNom: 'Math', semestre: 'S1', coefficient: 2, filiereId: 1, filiereCode: 'GI', filiereIntitule: 'Genie informatique', niveau: '1A' } })
      if (url === '/enseignant/mes-etudiants') return Promise.resolve({ data: [] })
      if (url === '/enseignant/suivi') return Promise.resolve({ data: [
        { caseId: 1, etudiantId: 10, studentName: 'Sara Amrani', motif: 'Risque note', priority: 'High', column: 'A voir', lastAction: null, creeLe: '2026-06-30T10:00:00Z' },
        { caseId: 2, etudiantId: 11, studentName: 'Yassine Bennani', motif: 'Absences', priority: 'Medium', column: 'En suivi', lastAction: 'Relance faite', creeLe: '2026-06-30T10:00:00Z' },
        { caseId: 3, etudiantId: 12, studentName: 'Imane Fassi', motif: 'Regle', priority: 'Low', column: 'Traite', lastAction: 'OK', creeLe: '2026-06-30T10:00:00Z' },
      ] })
      return Promise.resolve({ data: [] })
    })
  })

  it('renders the three teacher follow-up columns', async () => {
    renderPage()
    expect(await screen.findByRole('heading', { name: 'Suivi etudiants' })).toBeInTheDocument()
    expect(screen.getByRole('region', { name: 'A voir' })).toBeInTheDocument()
    expect(screen.getByRole('region', { name: 'En suivi' })).toBeInTheDocument()
    expect(screen.getByRole('region', { name: 'Traite' })).toBeInTheDocument()
  })

  it('adds an observation from a card', async () => {
    const user = userEvent.setup()
    renderPage()
    const column = await screen.findByRole('region', { name: 'A voir' })
    await user.click(within(column).getByRole('button', { name: /Ajouter observation/ }))
    await user.type(screen.getByLabelText('Observation'), 'Rendu ameliore.')
    await user.click(screen.getByRole('button', { name: 'Enregistrer' }))
    expect(postMock).toHaveBeenCalledWith('/enseignant/suivi/1/observation', { contenu: 'Rendu ameliore.' })
  })
})
```

- [ ] **Step 2: Run test to verify it fails**

Run:

```powershell
cd frontend
npm test -- Enseignant.test.tsx
```

Expected: FAIL because `Suivi etudiants` is not rendered.

- [ ] **Step 3: Add service calls**

In `frontend/src/services/interventions.ts`, add:

```ts
export interface TeacherFollowUpCard {
  caseId: number
  etudiantId: number
  studentName: string
  motif: string
  priority: string
  column: 'A voir' | 'En suivi' | 'Traite'
  lastAction: string | null
  creeLe: string
}

export const fetchTeacherFollowUps = () =>
  api.get<TeacherFollowUpCard[]>('/enseignant/suivi').then(r => r.data)

export const addTeacherObservation = (caseId: number, contenu: string) =>
  api.post(`/enseignant/suivi/${caseId}/observation`, { contenu })

export const markTeacherTreated = (caseId: number, contenu: string) =>
  api.post(`/enseignant/suivi/${caseId}/treated`, { contenu })

export const requestTeacherIntervention = (caseId: number, contenu: string) =>
  api.post(`/enseignant/suivi/${caseId}/request-intervention`, { contenu })
```

- [ ] **Step 4: Add the board to `Enseignant.tsx`**

Import the new service calls:

```ts
import {
  fetchTeacherFollowUps, addTeacherObservation, markTeacherTreated, requestTeacherIntervention,
  type TeacherFollowUpCard,
} from '../services/interventions'
```

Add state and mutations near the existing form state:

```ts
const [followUpAction, setFollowUpAction] = useState<{ card: TeacherFollowUpCard; action: 'observation' | 'treated' | 'request' } | null>(null)
const [followUpText, setFollowUpText] = useState('')

const { data: followUps = [] } = useQuery({
  queryKey: ['enseignant-suivi'],
  queryFn: fetchTeacherFollowUps,
  enabled: !!monModule,
})

const saveFollowUp = async () => {
  if (!followUpAction) return
  if (followUpAction.action === 'observation') {
    await addTeacherObservation(followUpAction.card.caseId, followUpText.trim())
  } else if (followUpAction.action === 'request') {
    await requestTeacherIntervention(followUpAction.card.caseId, followUpText.trim())
  } else {
    await markTeacherTreated(followUpAction.card.caseId, followUpText.trim())
  }
  setFollowUpAction(null)
  setFollowUpText('')
  queryClient.invalidateQueries({ queryKey: ['enseignant-suivi'] })
}
```

Render this section after the header and before KPIs:

```tsx
<section className="space-y-3">
  <div>
    <div className="cap mb-1">Interventions legeres</div>
    <h2 className="text-[18px] font-semibold tracking-tight">Suivi etudiants</h2>
  </div>
  <div className="grid grid-cols-1 gap-3 lg:grid-cols-3">
    {(['A voir', 'En suivi', 'Traite'] as const).map(column => {
      const items = followUps.filter(f => f.column === column)
      return (
        <section key={column} role="region" aria-label={column} className="card overflow-hidden">
          <div className="px-3 py-2.5 flex items-center justify-between" style={{ borderBottom: '1px solid var(--border)' }}>
            <h3 className="text-[13px] font-semibold">{column}</h3>
            <span className="cap font-mono">{items.length}</span>
          </div>
          {items.length === 0 && <div className="px-3 py-6 cap text-center">Aucun suivi.</div>}
          {items.map(card => (
            <div key={card.caseId} className="px-3 py-3" style={{ borderBottom: '1px solid var(--border)' }}>
              <div className="text-[12.5px] font-medium">{card.studentName}</div>
              <div className="cap mt-1">{card.motif}</div>
              {card.lastAction && <div className="text-[12px] mt-2" style={{ color: 'var(--text-2)' }}>{card.lastAction}</div>}
              {column !== 'Traite' && (
                <div className="flex gap-1.5 mt-3">
                  <button className="btn btn-sm" onClick={() => { setFollowUpAction({ card, action: 'observation' }); setFollowUpText('') }}>
                    <Icon name="plus" size={13} /> Ajouter observation
                  </button>
                  <button className="btn btn-sm" onClick={() => { setFollowUpAction({ card, action: 'request' }); setFollowUpText('') }}>
                    <Icon name="bookmark" size={13} /> Demander intervention
                  </button>
                  <button className="btn btn-sm btn-ghost" onClick={() => { setFollowUpAction({ card, action: 'treated' }); setFollowUpText('') }}>
                    <Icon name="check" size={13} /> Marquer traite
                  </button>
                </div>
              )}
            </div>
          ))}
        </section>
      )
    })}
  </div>
</section>
```

Import the existing modal:

```ts
import { Modal } from '../components/ui/Modal'
```

Render this modal near the existing page root:

```tsx
<Modal
  open={followUpAction !== null}
  onClose={() => setFollowUpAction(null)}
  title={followUpAction?.action === 'request' ? 'Demander intervention' : followUpAction?.action === 'observation' ? 'Ajouter observation' : 'Marquer traite'}
>
  {followUpAction && (
    <>
    <label className="flex flex-col gap-1">
      <span className="cap">Observation</span>
      <textarea className="input" rows={3} value={followUpText} onChange={e => setFollowUpText(e.target.value)} />
    </label>
    <div className="flex justify-end gap-2 mt-3">
      <button className="btn btn-sm" onClick={() => setFollowUpAction(null)}>Annuler</button>
      <button className="btn btn-sm btn-primary" onClick={saveFollowUp} disabled={followUpAction.action === 'observation' && !followUpText.trim()}>
        Enregistrer
      </button>
    </div>
    </>
  )}
</Modal>
```

- [ ] **Step 5: Run frontend teacher test**

Run:

```powershell
cd frontend
npm test -- Enseignant.test.tsx
```

Expected: PASS.

- [ ] **Step 6: Commit**

```powershell
git add frontend/src/services/interventions.ts frontend/src/pages/Enseignant.tsx frontend/src/pages/Enseignant.test.tsx
git commit -m "feat(interventions): add teacher follow-up board"
```

## Task 3: Admin Next-Action Board

**Files:**
- Modify: `frontend/src/pages/Cases.tsx`
- Test: `frontend/src/pages/Cases.test.tsx`

- [ ] **Step 1: Update failing test expectations**

In `frontend/src/pages/Cases.test.tsx`, change the column-label test to:

```tsx
it('renders the four admin next-action columns', async () => {
  renderCases()
  expect(await screen.findByRole('heading', { name: 'Interventions' })).toBeInTheDocument()
  expect(screen.getByRole('heading', { name: 'Nouveau' })).toBeInTheDocument()
  expect(screen.getByRole('heading', { name: 'A contacter' })).toBeInTheDocument()
  expect(screen.getByRole('heading', { name: 'Rendez-vous' })).toBeInTheDocument()
  expect(screen.getByRole('heading', { name: 'Resolu' })).toBeInTheDocument()
})
```

Add one mapping test:

```tsx
it('puts unassigned open cases in Nouveau and assigned open cases in A contacter', async () => {
  fetchCasesMock.mockResolvedValue([
    buildCase({ id: 21, etat: 'Open', ownerId: null, motif: 'Non assigne', etudiant: { id: 1, prenom: 'Ali', nom: 'Naji' } }),
    buildCase({ id: 22, etat: 'Open', ownerId: 2, motif: 'Assigne', etudiant: { id: 2, prenom: 'Mina', nom: 'Bali' } }),
  ])
  renderCases()
  expect(await screen.findByRole('button', { name: /Ali Naji/ })).toBeInTheDocument()
  expect(within(screen.getByRole('region', { name: 'Nouveau' })).getByRole('button', { name: /Ali Naji/ })).toBeInTheDocument()
  expect(within(screen.getByRole('region', { name: 'A contacter' })).getByRole('button', { name: /Mina Bali/ })).toBeInTheDocument()
})
```

- [ ] **Step 2: Run test to verify it fails**

Run:

```powershell
cd frontend
npm test -- Cases.test.tsx
```

Expected: FAIL because current labels are outreach-state labels.

- [ ] **Step 3: Change board columns and mapping**

In `frontend/src/pages/Cases.tsx`, replace `PRIMARY_STAGES` with:

```ts
const PRIMARY_STAGES = [
  { key: 'new', label: 'Nouveau' },
  { key: 'contact', label: 'A contacter' },
  { key: 'meeting', label: 'Rendez-vous' },
  { key: 'resolved', label: 'Resolu' },
] as const

function stageFor(c: InterventionCase) {
  if ((c.etat === 'Resolved') || (c.etat === 'Closed')) return 'resolved'
  if (c.etat === 'WaitingStudent' || (c.meetingScheduledFor && !c.meetingAttendance)) return 'meeting'
  if (c.etat === 'Open' && c.ownerId == null) return 'new'
  return 'contact'
}
```

Replace the `StageFilter` type with:

```ts
type StageFilter = 'all' | 'new' | 'contact' | 'meeting' | 'resolved'
```

Replace `primaryColumns` with:

```ts
const primaryColumns = PRIMARY_STAGES.map(s => ({
  ...s,
  items: cases
    .filter(c => stageFor(c) === s.key && passCommon(c) && (stage === 'all' || stage === s.key))
    .sort((a, b) => +new Date(b.creeLe) - +new Date(a.creeLe)),
}))
```

Update the stage dropdown:

```tsx
{PRIMARY_STAGES.map(s => <option key={s.key} value={s.key}>{s.label}</option>)}
```

Update the rendered column key:

```tsx
key={col.key}
```

Keep the card click behavior unchanged.

- [ ] **Step 4: Run frontend admin-board test**

Run:

```powershell
cd frontend
npm test -- Cases.test.tsx
```

Expected: PASS.

- [ ] **Step 5: Commit**

```powershell
git add frontend/src/pages/Cases.tsx frontend/src/pages/Cases.test.tsx
git commit -m "feat(interventions): simplify admin board columns"
```

## Task 4: Admin Detail Next Action

**Files:**
- Modify: `frontend/src/pages/CaseDetail.tsx`
- Test: `frontend/src/pages/CaseDetail.test.tsx`

- [ ] **Step 1: Add focused detail test**

Append to `frontend/src/pages/CaseDetail.test.tsx`:

```tsx
describe('Admin next action panel', () => {
  it('maps case state to a single primary action label', () => {
    expect(nextActionLabel({ etat: 'Open', ownerId: null, meetingScheduledFor: null, meetingAttendance: null })).toBe('Assigner')
    expect(nextActionLabel({ etat: 'Open', ownerId: 2, meetingScheduledFor: null, meetingAttendance: null })).toBe('Envoyer invitation')
    expect(nextActionLabel({ etat: 'WaitingStudent', ownerId: 2, meetingScheduledFor: '2026-07-01T10:00:00Z', meetingAttendance: null })).toBe('Saisir resultat')
    expect(nextActionLabel({ etat: 'Resolved', ownerId: 2, meetingScheduledFor: null, meetingAttendance: 'Held' })).toBe('Consulter')
  })
})
```

Add this import at the top of `frontend/src/pages/CaseDetail.test.tsx`:

```tsx
import { nextActionLabel } from './CaseDetail'
```

- [ ] **Step 2: Run test to verify it fails**

Run:

```powershell
cd frontend
npm test -- CaseDetail.test.tsx
```

Expected: FAIL because `nextActionLabel` is not exported yet.

- [ ] **Step 3: Add a small next-action helper**

In `frontend/src/pages/CaseDetail.tsx`, add near constants:

```ts
export function nextActionLabel(c: { etat: string; ownerId: number | null; meetingScheduledFor: string | null; meetingAttendance: string | null }) {
  if (c.etat === 'Open' && c.ownerId == null) return 'Assigner'
  if (c.etat === 'Open' || c.etat === 'InProgress') return 'Envoyer invitation'
  if (c.etat === 'WaitingStudent' || (c.meetingScheduledFor && !c.meetingAttendance)) return 'Saisir resultat'
  if (c.etat === 'Resolved' || c.etat === 'Closed') return 'Consulter'
  return 'Gerer'
}
```

Render after the header card:

```tsx
<section className="card p-4" role="region" aria-label="Action principale">
  <div className="cap mb-1">Prochaine action</div>
  <div className="flex items-center justify-between gap-3">
    <div className="text-[15px] font-semibold">{nextActionLabel(c)}</div>
    {nextActionLabel(c) === 'Saisir resultat' && <Pill tone="info">Rendez-vous</Pill>}
    {nextActionLabel(c) === 'Consulter' && <Pill tone="ok">Termine</Pill>}
  </div>
</section>
```

Keep existing outreach composer, meeting form, notes, communications, and timeline below. The current advanced transitions are already in `<details>`, so do not add another advanced section.

- [ ] **Step 4: Run detail tests**

Run:

```powershell
cd frontend
npm test -- CaseDetail.test.tsx
```

Expected: PASS.

- [ ] **Step 5: Commit**

```powershell
git add frontend/src/pages/CaseDetail.tsx frontend/src/pages/CaseDetail.test.tsx
git commit -m "feat(interventions): highlight admin next action"
```

## Task 5: Final Verification

**Files:**
- No new files.

- [ ] **Step 1: Run backend focused tests**

```powershell
cd backend/PlateformePFA.Tests
dotnet test --nologo --filter "EnseignantFollowUpControllerTests|InterventionCasesControllerTests"
```

Expected: PASS.

- [ ] **Step 2: Run frontend focused tests**

```powershell
cd frontend
npm test -- Enseignant.test.tsx Cases.test.tsx CaseDetail.test.tsx
```

Expected: PASS.

- [ ] **Step 3: Run frontend build**

```powershell
cd frontend
npm run build
```

Expected: PASS with Vite build output.

- [ ] **Step 4: Check git status**

```powershell
git status --short
```

Expected: no uncommitted files.

## Self-Review

- Spec coverage: teacher board, admin board, role-specific simplification, hidden admin-only controls, and no drag/drop are covered.
- Red-flag scan: no task requires deferred fill-ins or a new workflow system.
- Type consistency: teacher DTO field names match frontend service field names; admin stages use stable string keys.

## Skipped

- Drag and drop. Add it only if users need manual card movement after the next-action board ships.
- New teacher follow-up table. Add it only if note markers become hard to query or report on.
- Full admin detail redesign. The primary action panel gives value without reopening every detail component.
