using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using PlateformePFA.Tests.Fixtures;
using Xunit;

namespace PlateformePFA.Tests.Controllers;

public class EtudiantsControllerTests : IClassFixture<TestWebFactory>
{
    private readonly TestWebFactory _factory;

    public EtudiantsControllerTests(TestWebFactory factory)
    {
        _factory = factory;
        _factory.SeedAdmin();
        using var ctx = _factory.CreateContext();
        SampleData.SeedOne(ctx);
    }

    [Fact]
    public async Task GetEtudiants_returns_paginated_list()
    {
        var client = _factory.CreateClient();
        var token = await AuthHelper.GetAdminTokenAsync(client);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var res = await client.GetAsync("/api/etudiants");
        res.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task GetWithStats_returns_students_with_risk_scores()
    {
        var client = _factory.CreateClient();
        var token = await AuthHelper.GetAdminTokenAsync(client);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var res = await client.GetAsync("/api/etudiants/with-stats");
        res.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await res.Content.ReadFromJsonAsync<List<EtudiantWithStats>>();
        body.Should().NotBeEmpty();
        body!.First().Matricule.Should().Be("E10001");
        body.First().Moyenne.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task GetEtudiantNotes_returns_module_notes()
    {
        var client = _factory.CreateClient();
        var token = await AuthHelper.GetAdminTokenAsync(client);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        using var ctx = _factory.CreateContext();
        var etu = ctx.Etudiants.First(e => e.Matricule == "E10001");

        var res = await client.GetAsync($"/api/etudiants/{etu.Id}/notes");
        res.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await res.Content.ReadFromJsonAsync<List<NoteRow>>();
        body.Should().NotBeEmpty();
    }

    [Fact]
    public async Task PostEtudiant_creates_student()
    {
        var client = _factory.CreateClient();
        var token = await AuthHelper.GetAdminTokenAsync(client);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        using var ctx = _factory.CreateContext();
        var fil = ctx.Filieres.First(f => f.Code == "GI");

        var res = await client.PostAsJsonAsync("/api/etudiants", new
        {
            matricule = "E99999",
            nom = "New",
            prenom = "Student",
            email = "new@test.com",
            filiereId = fil.Id,
            niveau = "CI1",
            annee = "2025/2026",
        });

        res.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    [Fact]
    public async Task PostEtudiant_rejects_duplicate_matricule()
    {
        var client = _factory.CreateClient();
        var token = await AuthHelper.GetAdminTokenAsync(client);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        using var ctx = _factory.CreateContext();
        var fil = ctx.Filieres.First(f => f.Code == "GI");

        var res = await client.PostAsJsonAsync("/api/etudiants", new
        {
            matricule = "E10001", // already exists
            nom = "Dup",
            prenom = "Test",
            filiereId = fil.Id,
            niveau = "CI1",
            annee = "2025/2026",
        });

        res.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    private record EtudiantWithStats(string Matricule, decimal Moyenne, decimal ScoreRisque);
    private record NoteRow(int ModuleId, string Code, string Nom);
}
