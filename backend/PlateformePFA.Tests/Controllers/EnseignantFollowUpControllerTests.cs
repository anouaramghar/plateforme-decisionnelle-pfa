using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using PlateformePFA.API.Models;
using PlateformePFA.API.Services;
using PlateformePFA.Tests.Fixtures;
using Xunit;

namespace PlateformePFA.Tests.Controllers;

public class EnseignantFollowUpControllerTests : IClassFixture<TestWebFactory>
{
    private readonly TestWebFactory _factory;
    private static int _seq;

    public EnseignantFollowUpControllerTests(TestWebFactory factory)
    {
        _factory = factory;
        _factory.SeedAdmin();
        using var ctx = _factory.CreateContext();
        SampleData.SeedOne(ctx);
    }

    private async Task<HttpClient> TeacherClientAsync(
        string email = "followup.teacher@eniad.ma",
        bool hasModule = true)
    {
        using (var ctx = _factory.CreateContext())
        {
            var module = ctx.Modules.Single(m => m.Code == "GI01");
            if (!ctx.Utilisateurs.Any(u => u.Email == email))
            {
                ctx.Utilisateurs.Add(new Utilisateur
                {
                    Email = email,
                    Nom = "Follow",
                    Prenom = "Teacher",
                    MotDePasseHash = BCrypt.Net.BCrypt.HashPassword("TeacherPass!2026"),
                    Role = "Enseignant",
                    EstActif = true,
                    ModuleId = hasModule ? module.Id : null,
                });
                ctx.SaveChanges();
            }
        }

        var client = _factory.CreateClient();
        var token = await AuthHelper.GetTokenAsync(client, email, "TeacherPass!2026");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    private int CreateCase(string niveau = "CI1", string etat = CaseWorkflowState.Open)
    {
        using var ctx = _factory.CreateContext();
        var fil = ctx.Filieres.Single(f => f.Code == "GI");
        var seq = Interlocked.Increment(ref _seq);
        var etu = new Etudiant
        {
            Matricule = $"EFU{seq:D5}",
            Nom = "Follow",
            Prenom = $"Student{seq}",
            FiliereId = fil.Id,
            Niveau = niveau,
            Annee = "2025/2026",
        };
        ctx.Etudiants.Add(etu);
        ctx.SaveChanges();

        var c = new InterventionCase
        {
            EtudiantId = etu.Id,
            FiliereId = etu.FiliereId,
            Motif = "Besoin de suivi",
            Priorite = "High",
            Etat = etat,
            CreeLe = DateTime.UtcNow.AddMinutes(seq),
        };
        ctx.InterventionCases.Add(c);
        ctx.SaveChanges();
        return c.Id;
    }

    private record FollowUpCard(int CaseId, string StudentName, string Column, string? LastAction);

    [Fact]
    public async Task Get_suivi_returns_teacher_cards_with_a_voir_for_fresh_case()
    {
        var caseId = CreateCase();
        var client = await TeacherClientAsync();

        var res = await client.GetAsync("/api/enseignant/suivi");

        res.StatusCode.Should().Be(HttpStatusCode.OK);
        var cards = await res.Content.ReadFromJsonAsync<List<FollowUpCard>>();
        cards!.Should().Contain(c =>
            c.CaseId == caseId &&
            c.StudentName.StartsWith("Follow Student") &&
            c.Column == "A voir");
    }

    [Fact]
    public async Task Get_suivi_returns_en_suivi_for_in_progress_case_without_teacher_note()
    {
        var caseId = CreateCase(etat: CaseWorkflowState.InProgress);
        var client = await TeacherClientAsync();

        var res = await client.GetAsync("/api/enseignant/suivi");

        res.StatusCode.Should().Be(HttpStatusCode.OK);
        var cards = await res.Content.ReadFromJsonAsync<List<FollowUpCard>>();
        cards!.Should().Contain(c => c.CaseId == caseId && c.Column == "En suivi");
    }

    [Fact]
    public async Task Get_suivi_returns_empty_for_teacher_with_no_module()
    {
        CreateCase();
        var client = await TeacherClientAsync("followup.nomodule@eniad.ma", hasModule: false);

        var cards = await client.GetFromJsonAsync<List<FollowUpCard>>("/api/enseignant/suivi");

        cards.Should().BeEmpty();
    }

    [Fact]
    public async Task Get_suivi_returns_traite_for_resolved_or_closed_case()
    {
        var resolvedCaseId = CreateCase(etat: CaseWorkflowState.Resolved);
        var closedCaseId = CreateCase(etat: CaseWorkflowState.Closed);
        var client = await TeacherClientAsync();

        var cards = await client.GetFromJsonAsync<List<FollowUpCard>>("/api/enseignant/suivi");

        cards!.Should().Contain(c => c.CaseId == resolvedCaseId && c.Column == "Traite");
        cards.Should().Contain(c => c.CaseId == closedCaseId && c.Column == "Traite");
    }

    [Fact]
    public async Task Get_suivi_returns_en_suivi_for_teacher_public_note()
    {
        var caseId = CreateCase();
        var client = await TeacherClientAsync();
        (await client.PostAsJsonAsync($"/api/enseignant/suivi/{caseId}/observation",
            new { contenu = "Relance faite" })).EnsureSuccessStatusCode();

        var cards = await client.GetFromJsonAsync<List<FollowUpCard>>("/api/enseignant/suivi");

        cards!.Should().Contain(c => c.CaseId == caseId && c.Column == "En suivi");
    }

    [Fact]
    public async Task Get_suivi_returns_latest_public_note_with_marker_stripped()
    {
        var caseId = CreateCase();
        using (var ctx = _factory.CreateContext())
        {
            ctx.CaseNotes.AddRange(
                new CaseNote
                {
                    CaseId = caseId,
                    Contenu = "[teacher-follow-up:requested] Ancienne action",
                    IsPrivate = false,
                    CreeLe = DateTime.UtcNow.AddMinutes(-2),
                },
                new CaseNote
                {
                    CaseId = caseId,
                    Contenu = "PRIVATE latest",
                    IsPrivate = true,
                    CreeLe = DateTime.UtcNow.AddMinutes(1),
                },
                new CaseNote
                {
                    CaseId = caseId,
                    Contenu = "[teacher-follow-up:treated] Derniere action",
                    IsPrivate = false,
                    CreeLe = DateTime.UtcNow,
                });
            ctx.SaveChanges();
        }
        var client = await TeacherClientAsync();

        var cards = await client.GetFromJsonAsync<List<FollowUpCard>>("/api/enseignant/suivi");

        cards!.Single(c => c.CaseId == caseId).LastAction.Should().Be("Derniere action");
    }

    [Fact]
    public async Task Observation_creates_public_note()
    {
        var caseId = CreateCase();
        var client = await TeacherClientAsync();

        var res = await client.PostAsJsonAsync($"/api/enseignant/suivi/{caseId}/observation",
            new { contenu = "Observation utile" });

        res.StatusCode.Should().Be(HttpStatusCode.Created);
        using var ctx = _factory.CreateContext();
        var note = ctx.CaseNotes.Single(n => n.CaseId == caseId);
        note.Contenu.Should().Be("Observation utile");
        note.IsPrivate.Should().BeFalse();
    }

    [Fact]
    public async Task Observation_too_long_returns_bad_request()
    {
        var caseId = CreateCase();
        var client = await TeacherClientAsync();

        var res = await client.PostAsJsonAsync($"/api/enseignant/suivi/{caseId}/observation",
            new { contenu = new string('x', 2001) });

        res.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        using var ctx = _factory.CreateContext();
        ctx.CaseNotes.Should().NotContain(n => n.CaseId == caseId);
    }

    [Fact]
    public async Task Request_intervention_composed_too_long_returns_bad_request()
    {
        var caseId = CreateCase();
        var client = await TeacherClientAsync();

        var res = await client.PostAsJsonAsync($"/api/enseignant/suivi/{caseId}/request-intervention",
            new { contenu = new string('x', 2000) });

        res.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        using var ctx = _factory.CreateContext();
        ctx.CaseNotes.Should().NotContain(n => n.CaseId == caseId);
    }

    [Fact]
    public async Task Treated_creates_marker_note()
    {
        var caseId = CreateCase();
        var client = await TeacherClientAsync();

        var res = await client.PostAsJsonAsync($"/api/enseignant/suivi/{caseId}/treated",
            new { contenu = "" });

        res.StatusCode.Should().Be(HttpStatusCode.Created);
        using var ctx = _factory.CreateContext();
        var note = ctx.CaseNotes.Single(n => n.CaseId == caseId);
        note.Contenu.Should().StartWith("[teacher-follow-up:treated]");
        note.IsPrivate.Should().BeFalse();
    }

    [Fact]
    public async Task Request_intervention_creates_marker_note()
    {
        var caseId = CreateCase();
        var client = await TeacherClientAsync();

        var res = await client.PostAsJsonAsync($"/api/enseignant/suivi/{caseId}/request-intervention",
            new { contenu = "" });

        res.StatusCode.Should().Be(HttpStatusCode.Created);
        using var ctx = _factory.CreateContext();
        var note = ctx.CaseNotes.Single(n => n.CaseId == caseId);
        note.Contenu.Should().StartWith("[teacher-follow-up:requested]");
        note.IsPrivate.Should().BeFalse();
    }

    [Fact]
    public async Task Action_outside_cohort_returns_not_found()
    {
        var caseId = CreateCase("CI3");
        var client = await TeacherClientAsync();

        var res = await client.PostAsJsonAsync($"/api/enseignant/suivi/{caseId}/observation",
            new { contenu = "Invisible" });

        res.StatusCode.Should().Be(HttpStatusCode.NotFound);
        using var ctx = _factory.CreateContext();
        ctx.CaseNotes.Should().NotContain(n => n.CaseId == caseId);
    }
}
