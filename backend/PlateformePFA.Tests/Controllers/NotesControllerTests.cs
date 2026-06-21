using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using PlateformePFA.API.Models;
using PlateformePFA.Tests.Fixtures;
using Xunit;

namespace PlateformePFA.Tests.Controllers;

public class NotesControllerTests : IClassFixture<TestWebFactory>
{
    private readonly TestWebFactory _factory;

    public NotesControllerTests(TestWebFactory factory)
    {
        _factory = factory;
        _factory.SeedAdmin();
        using var ctx = _factory.CreateContext();
        SampleData.SeedOne(ctx);
    }

    [Fact]
    public async Task Upsert_creates_new_natural_key()
    {
        var client = await CreateAdminClientAsync();
        using var seedContext = _factory.CreateContext();
        var studentId = seedContext.Etudiants.Single(e => e.Matricule == "E10001").Id;
        var moduleId = seedContext.Modules.Single(m => m.Code == "GI01").Id;

        var response = await client.PutAsJsonAsync("/api/notes/upsert", new
        {
            etudiantId = studentId,
            moduleId,
            noteFinal = 9m,
            annee = "2026/2027",
            semestre = "S1",
        });

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        using var verifyContext = _factory.CreateContext();
        verifyContext.Notes.Count(n =>
            n.EtudiantId == studentId && n.ModuleId == moduleId &&
            n.Annee == "2026/2027" && n.Semestre == "S1").Should().Be(1);
    }

    [Fact]
    public async Task Upsert_updates_matching_natural_key_without_duplicate()
    {
        var client = await CreateAdminClientAsync();
        using var seedContext = _factory.CreateContext();
        var studentId = seedContext.Etudiants.Single(e => e.Matricule == "E10001").Id;
        var moduleId = seedContext.Modules.Single(m => m.Code == "GI01").Id;

        var response = await client.PutAsJsonAsync("/api/notes/upsert", new
        {
            etudiantId = studentId,
            moduleId,
            noteFinal = 15m,
            annee = "2025/2026",
            semestre = "S2",
        });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        using var verifyContext = _factory.CreateContext();
        var matching = verifyContext.Notes.Where(n =>
            n.EtudiantId == studentId && n.ModuleId == moduleId &&
            n.Annee == "2025/2026" && n.Semestre == "S2").ToList();
        matching.Should().ContainSingle();
        matching.Single().NoteFinal.Should().Be(15m);
    }

    [Fact]
    public async Task Upsert_rejects_withdrawn_student()
    {
        int studentId;
        int moduleId;
        using (var context = _factory.CreateContext())
        {
            var seeded = SampleData.SeedOne(context);
            var withdrawn = new Etudiant
            {
                Matricule = "E-WITHDRAWN-NOTE",
                Nom = "Retire",
                Prenom = "Note",
                FiliereId = seeded.Filiere.Id,
                Niveau = "CI1",
                Annee = "2025/2026",
                DesinscritLe = DateTime.UtcNow.AddDays(-1),
            };
            context.Etudiants.Add(withdrawn);
            context.SaveChanges();
            studentId = withdrawn.Id;
            moduleId = seeded.Module.Id;
        }

        var client = await CreateAdminClientAsync();
        var response = await client.PutAsJsonAsync("/api/notes/upsert", new
        {
            etudiantId = studentId,
            moduleId,
            noteFinal = 12m,
            annee = "2025/2026",
            semestre = "S1",
        });

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    private async Task<HttpClient> CreateAdminClientAsync()
    {
        var client = _factory.CreateClient();
        var token = await AuthHelper.GetAdminTokenAsync(client);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }
}
