using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using PlateformePFA.API.Data;
using PlateformePFA.API.Models;
using PlateformePFA.API.Services;
using PlateformePFA.Tests.Fixtures;
using Xunit;

namespace PlateformePFA.Tests.Controllers;

/// <summary>
/// Tests for the intervention-case write surface used by the Intervention
/// Copilot confirmation layer: case creation (existing endpoint, exercised
/// here as the "create case" outcome) and link-signals (the duplicate-case
/// prevention path). Confirmed actions must record the user as actor and
/// emit an immutable timeline event.
/// </summary>
public class InterventionCasesControllerTests : IClassFixture<TestWebFactory>
{
    private readonly TestWebFactory _factory;

    public InterventionCasesControllerTests(TestWebFactory factory)
    {
        _factory = factory;
        _factory.SeedAdmin();
        using var ctx = _factory.CreateContext();
        SampleData.SeedOne(ctx);
    }

    private async Task<HttpClient> AuthedClientAsync()
    {
        var client = _factory.CreateClient();
        var token = await AuthHelper.GetAdminTokenAsync(client);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    // Provisions a FRESH student per call. The one-active-case-per-student guard
    // means reusing a single seeded student would make sibling tests collide in
    // this class's shared InMemory store. Placed in the GI/CI1 cohort so the
    // teacher-scope tests still match. Static counter keeps matricules unique
    // across test methods (xUnit builds a new test-class instance per test).
    private static int _studentSeq;
    private static int StudentId(AppDbContext ctx)
    {
        var fil = ctx.Filieres.First(f => f.Code == "GI");
        var seq = System.Threading.Interlocked.Increment(ref _studentSeq);
        var e = new Etudiant
        {
            Matricule = $"EDUP{seq:D5}", Nom = "Dup", Prenom = $"Stud{seq}",
            FiliereId = fil.Id, Niveau = "CI1", Annee = "2025/2026",
        };
        ctx.Etudiants.Add(e);
        ctx.SaveChanges();
        return e.Id;
    }

    private record CaseResponse(int Id);

    private async Task<int> CreateCaseAsync(HttpClient client, int etuId, int? alerteId = null)
    {
        var res = await client.PostAsJsonAsync("/api/intervention-cases", new
        {
            etudiantId = etuId,
            motif = "Copilot recommendation",
            priorite = "High",
            alerteId = alerteId,
        });
        res.StatusCode.Should().Be(HttpStatusCode.Created);
        var body = await res.Content.ReadFromJsonAsync<CaseResponse>();
        return body!.Id;
    }

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

    private async Task<int> CreateSignalAsync(AppDbContext ctx, int etuId)
    {
        var a = new Alerte
        {
            EtudiantId = etuId, Type = "RisqueEchec", Niveau = "Eleve",
            Message = "signal", Resolue = false, CreeLe = DateTime.UtcNow,
        };
        ctx.Alertes.Add(a);
        await ctx.SaveChangesAsync();
        return a.Id;
    }

    [Fact]
    public async Task Link_signals_links_alertes_to_open_case_and_records_timeline()
    {
        var client = await AuthedClientAsync();
        using var ctx = _factory.CreateContext();
        var etuId = StudentId(ctx);

        var caseId = await CreateCaseAsync(client, etuId);
        var s1 = await CreateSignalAsync(ctx, etuId);
        var s2 = await CreateSignalAsync(ctx, etuId);

        var res = await client.PostAsJsonAsync(
            $"/api/intervention-cases/{caseId}/link-signals",
            new { alerteIds = new[] { s1, s2 } });

        res.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // Both signals now point at the case.
        using var verify = _factory.CreateContext();
        verify.Alertes.Single(a => a.Id == s1).CaseId.Should().Be(caseId);
        verify.Alertes.Single(a => a.Id == s2).CaseId.Should().Be(caseId);
        // A SignalLinked timeline event was appended.
        verify.CaseTimelineEvents.Should().Contain(e =>
            e.CaseId == caseId && e.Action == "SignalLinked");
    }

    [Fact]
    public async Task Link_signals_rejects_signal_from_a_different_student()
    {
        var client = await AuthedClientAsync();
        using var ctx = _factory.CreateContext();
        var etuId = StudentId(ctx);

        // Create a second student with a signal.
        var fil = ctx.Filieres.First();
        var other = new Etudiant
        {
            Matricule = "E10099", Nom = "Other", Prenom = "Student",
            FiliereId = fil.Id, Niveau = "CI1", Annee = "2025/2026",
        };
        ctx.Etudiants.Add(other);
        await ctx.SaveChangesAsync();
        var otherSignal = await CreateSignalAsync(ctx, other.Id);

        var caseId = await CreateCaseAsync(client, etuId);

        var res = await client.PostAsJsonAsync(
            $"/api/intervention-cases/{caseId}/link-signals",
            new { alerteIds = new[] { otherSignal } });

        res.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        // The foreign signal must not have been linked.
        using var verify = _factory.CreateContext();
        verify.Alertes.Single(a => a.Id == otherSignal).CaseId.Should().BeNull();
    }

    [Fact]
    public async Task Link_signals_rejects_already_linked_signal_all_or_nothing()
    {
        var client = await AuthedClientAsync();
        using var ctx = _factory.CreateContext();
        var etuId = StudentId(ctx);

        var caseId = await CreateCaseAsync(client, etuId);
        var linkedAlready = await CreateSignalAsync(ctx, etuId);
        // Pre-link one signal to a different case.
        ctx.Alertes.Single(a => a.Id == linkedAlready).CaseId = caseId;
        await ctx.SaveChangesAsync();

        var fresh = await CreateSignalAsync(ctx, etuId);

        var res = await client.PostAsJsonAsync(
            $"/api/intervention-cases/{caseId}/link-signals",
            new { alerteIds = new[] { linkedAlready, fresh } });

        res.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        // The fresh signal must NOT be linked — all-or-nothing.
        using var verify = _factory.CreateContext();
        verify.Alertes.Single(a => a.Id == fresh).CaseId.Should().BeNull();
    }

    [Fact]
    public async Task Link_signals_rejects_resolved_case_as_target()
    {
        var client = await AuthedClientAsync();
        using var ctx = _factory.CreateContext();
        var etuId = StudentId(ctx);

        var caseId = await CreateCaseAsync(client, etuId);
        // Drive the case to InProgress then Resolved (state machine requires owner + outcome).
        var owner = ctx.Utilisateurs.First(u => u.Email == "test-admin@eniad.ma").Id;
        await client.PatchAsJsonAsync($"/api/intervention-cases/{caseId}/transition",
            new { etat = CaseWorkflowState.InProgress, ownerId = owner });
        using (var prepare = _factory.CreateContext())
        {
            prepare.InterventionCases.Single(c => c.Id == caseId).MeetingAttendance = "Held";
            prepare.SaveChanges();
        }
        await client.PatchAsJsonAsync($"/api/intervention-cases/{caseId}/transition",
            new
            {
                etat = CaseWorkflowState.Resolved,
                outcome = "Improved",
                resolutionSummary = "Suivi concluant",
            });

        var s = await CreateSignalAsync(ctx, etuId);
        var res = await client.PostAsJsonAsync(
            $"/api/intervention-cases/{caseId}/link-signals",
            new { alerteIds = new[] { s } });

        res.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Link_signals_empty_ids_returns_bad_request()
    {
        var client = await AuthedClientAsync();
        using var ctx = _factory.CreateContext();
        var etuId = StudentId(ctx);
        var caseId = await CreateCaseAsync(client, etuId);

        var res = await client.PostAsJsonAsync(
            $"/api/intervention-cases/{caseId}/link-signals",
            new { alerteIds = Array.Empty<int>() });

        res.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Create_case_returns_conflict_with_existing_active_case()
    {
        var client = await AuthedClientAsync();
        int studentId;
        using (var db = _factory.CreateContext()) studentId = StudentId(db);
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

    [Fact]
    public async Task Create_case_records_user_as_actor_in_timeline()
    {
        var client = await AuthedClientAsync();
        using var ctx = _factory.CreateContext();
        var etuId = StudentId(ctx);

        var caseId = await CreateCaseAsync(client, etuId);

        using var verify = _factory.CreateContext();
        var created = verify.CaseTimelineEvents.Single(e => e.CaseId == caseId && e.Action == "Created");
        created.UtilisateurNom.Should().NotBeNullOrEmpty();
        created.UtilisateurId.Should().HaveValue();
    }

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
}
