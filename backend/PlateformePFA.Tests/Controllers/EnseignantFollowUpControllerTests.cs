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

    private async Task<HttpClient> TeacherClientAsync()
    {
        using (var ctx = _factory.CreateContext())
        {
            var module = ctx.Modules.Single(m => m.Code == "GI01");
            if (!ctx.Utilisateurs.Any(u => u.Email == "followup.teacher@eniad.ma"))
            {
                ctx.Utilisateurs.Add(new Utilisateur
                {
                    Email = "followup.teacher@eniad.ma",
                    Nom = "Follow",
                    Prenom = "Teacher",
                    MotDePasseHash = BCrypt.Net.BCrypt.HashPassword("TeacherPass!2026"),
                    Role = "Enseignant",
                    EstActif = true,
                    ModuleId = module.Id,
                });
                ctx.SaveChanges();
            }
        }

        var client = _factory.CreateClient();
        var token = await AuthHelper.GetTokenAsync(client, "followup.teacher@eniad.ma", "TeacherPass!2026");
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

    private record FollowUpCard(int CaseId, string StudentName, string Column);

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
