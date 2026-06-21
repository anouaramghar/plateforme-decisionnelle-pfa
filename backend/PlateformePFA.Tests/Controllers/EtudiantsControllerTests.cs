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
        body!.First().Moyenne.Should().BeGreaterThan(0);
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

    [Fact]
    public async Task GetEtudiant_rejects_withdrawn_student()
    {
        var withdrawnId = CreateWithdrawnStudent("E-WITHDRAWN-GET");
        var client = await CreateAdminClientAsync();

        var response = await client.GetAsync($"/api/etudiants/{withdrawnId}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GetEtudiantNotes_rejects_withdrawn_student()
    {
        var withdrawnId = CreateWithdrawnStudent("E-WITHDRAWN-NOTES");
        var client = await CreateAdminClientAsync();

        var response = await client.GetAsync($"/api/etudiants/{withdrawnId}/notes");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task PutEtudiant_rejects_withdrawn_student()
    {
        var withdrawnId = CreateWithdrawnStudent("E-WITHDRAWN-PUT");
        using var context = _factory.CreateContext();
        var filiereId = context.Filieres.Single(f => f.Code == "GI").Id;
        var client = await CreateAdminClientAsync();

        var response = await client.PutAsJsonAsync($"/api/etudiants/{withdrawnId}", new
        {
            matricule = "E-WITHDRAWN-PUT",
            nom = "Changed",
            prenom = "Student",
            email = "changed@test.com",
            filiereId,
            niveau = "CI1",
            annee = "2025/2026",
        });

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task DeleteEtudiant_preserves_original_withdrawal_timestamp_when_repeated()
    {
        int studentId;
        using (var context = _factory.CreateContext())
        {
            var seeded = SampleData.SeedOne(context);
            var student = new PlateformePFA.API.Models.Etudiant
            {
                Matricule = "E-DELETE-IDEMPOTENT",
                Nom = "Delete",
                Prenom = "Once",
                FiliereId = seeded.Filiere.Id,
                Niveau = "CI1",
                Annee = "2025/2026",
            };
            context.Etudiants.Add(student);
            context.SaveChanges();
            studentId = student.Id;
        }

        var client = await CreateAdminClientAsync();
        (await client.DeleteAsync($"/api/etudiants/{studentId}")).StatusCode.Should().Be(HttpStatusCode.NoContent);
        DateTime firstWithdrawal;
        using (var context = _factory.CreateContext())
            firstWithdrawal = context.Etudiants.Single(e => e.Id == studentId).DesinscritLe!.Value;

        await Task.Delay(20);
        (await client.DeleteAsync($"/api/etudiants/{studentId}")).StatusCode.Should().Be(HttpStatusCode.NoContent);

        using var verifyContext = _factory.CreateContext();
        verifyContext.Etudiants.Single(e => e.Id == studentId).DesinscritLe.Should().Be(firstWithdrawal);
    }

    [Fact]
    public async Task ImportEtudiants_creates_complete_valid_batch()
    {
        var client = await CreateAdminClientAsync();
        var response = await client.PostAsJsonAsync("/api/etudiants/import", new[]
        {
            new { rowNumber = 2, matricule = "E-IMPORT-001", nom = "Import", prenom = "One", email = "one@test.com", filiereCode = "GI", niveau = "CI1", annee = "2025/2026" },
            new { rowNumber = 3, matricule = "E-IMPORT-002", nom = "Import", prenom = "Two", email = "two@test.com", filiereCode = "GI", niveau = "CI2", annee = "2025/2026" },
        });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<ImportResult>();
        result!.Imported.Should().Be(2);
        result.Errors.Should().BeEmpty();
        using var context = _factory.CreateContext();
        context.Etudiants.Count(e => e.Matricule == "E-IMPORT-001" || e.Matricule == "E-IMPORT-002").Should().Be(2);
    }

    [Fact]
    public async Task ImportEtudiants_rejects_entire_batch_when_any_row_is_invalid()
    {
        var client = await CreateAdminClientAsync();
        var response = await client.PostAsJsonAsync("/api/etudiants/import", new[]
        {
            new { rowNumber = 2, matricule = "E-IMPORT-ROLLBACK", nom = "Valid", prenom = "Row", email = "valid@test.com", filiereCode = "GI", niveau = "CI1", annee = "2025/2026" },
            new { rowNumber = 3, matricule = "E10001", nom = "Duplicate", prenom = "Row", email = "duplicate@test.com", filiereCode = "GI", niveau = "CI1", annee = "2025/2026" },
        });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var result = await response.Content.ReadFromJsonAsync<ImportResult>();
        result!.Imported.Should().Be(0);
        result.Errors.Should().Contain(e => e.RowNumber == 3 && e.Field == "matricule");
        using var context = _factory.CreateContext();
        context.Etudiants.Should().NotContain(e => e.Matricule == "E-IMPORT-ROLLBACK");
    }

    private int CreateWithdrawnStudent(string matricule)
    {
        using var context = _factory.CreateContext();
        var seeded = SampleData.SeedOne(context);
        var existing = context.Etudiants.SingleOrDefault(e => e.Matricule == matricule);
        if (existing != null) return existing.Id;
        var student = new PlateformePFA.API.Models.Etudiant
        {
            Matricule = matricule,
            Nom = "Withdrawn",
            Prenom = "Student",
            FiliereId = seeded.Filiere.Id,
            Niveau = "CI1",
            Annee = "2025/2026",
            DesinscritLe = DateTime.UtcNow.AddDays(-1),
        };
        context.Etudiants.Add(student);
        context.SaveChanges();
        return student.Id;
    }

    private async Task<HttpClient> CreateAdminClientAsync()
    {
        var client = _factory.CreateClient();
        var token = await AuthHelper.GetAdminTokenAsync(client);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    private record EtudiantWithStats(string Matricule, decimal Moyenne, decimal ScoreRisque);
    private record NoteRow(int ModuleId, string Code, string Nom);
    private record ImportError(int RowNumber, string Field, string Message);
    private record ImportResult(int Imported, List<ImportError> Errors);
}
