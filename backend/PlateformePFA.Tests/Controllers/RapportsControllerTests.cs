using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using PlateformePFA.Tests.Fixtures;
using PlateformePFA.API.Models;
using System.Text;
using Xunit;

namespace PlateformePFA.Tests.Controllers;

public class RapportsControllerTests : IClassFixture<TestWebFactory>
{
    private readonly TestWebFactory _factory;

    public RapportsControllerTests(TestWebFactory factory)
    {
        _factory = factory;
        _factory.SeedAdmin();
        using var ctx = _factory.CreateContext();
        SampleData.SeedOne(ctx);
    }

    [Theory]
    [InlineData("perf-globale", "PDF",  "application/pdf")]
    [InlineData("notes",        "XLSX", "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet")]
    [InlineData("absences",     "CSV",  "text/csv")]
    public async Task Generate_then_download_returns_correct_content_type(
        string template, string format, string expectedContentType)
    {
        var client = _factory.CreateClient();
        var token = await AuthHelper.GetAdminTokenAsync(client);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var create = await client.PostAsJsonAsync("/api/rapports", new
        {
            templateId = template,
            format = format,
            filiereCode = "TOUS",
            periode = "S2 2025/2026",
        });
        create.StatusCode.Should().Be(HttpStatusCode.OK);
        var meta = await create.Content.ReadFromJsonAsync<RapportMeta>();
        meta!.Taille.Should().BeGreaterThan(0);

        var dl = await client.GetAsync($"/api/rapports/{meta.Id}/download");
        dl.StatusCode.Should().Be(HttpStatusCode.OK);
        dl.Content.Headers.ContentType!.ToString().Should().StartWith(expectedContentType);
        var bytes = await dl.Content.ReadAsByteArrayAsync();
        bytes.Length.Should().Be(meta.Taille);
    }

    [Fact]
    public async Task BilanMl_excludes_withdrawn_students()
    {
        using (var context = _factory.CreateContext())
        {
            var seeded = SampleData.SeedOne(context);
            var withdrawn = new Etudiant
            {
                Matricule = "E-WITHDRAWN-REPORT", Nom = "Report", Prenom = "Withdrawn",
                FiliereId = seeded.Filiere.Id, Niveau = "CI1", Annee = "2025/2026",
                DesinscritLe = DateTime.UtcNow.AddDays(-1),
            };
            context.Etudiants.Add(withdrawn);
            context.SaveChanges();
        }

        var client = _factory.CreateClient();
        var token = await AuthHelper.GetAdminTokenAsync(client);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var create = await client.PostAsJsonAsync("/api/rapports", new
        {
            templateId = "bilan-ml",
            format = "CSV",
            filiereCode = "TOUS",
            periode = "S2 2025/2026",
        });
        var meta = await create.Content.ReadFromJsonAsync<RapportMeta>();
        var download = await client.GetAsync($"/api/rapports/{meta!.Id}/download");
        var csv = Encoding.UTF8.GetString(await download.Content.ReadAsByteArrayAsync());

        csv.Should().Contain("1 étudiants évalués");
        csv.Should().NotContain("2 étudiants évalués");
    }

    [Fact]
    public async Task Teacher_cannot_generate_report_for_other_module_or_TOUS()
    {
        int modAId;
        using (var context = _factory.CreateContext())
        {
            var seeded = SampleData.SeedOne(context);
            modAId = seeded.Module.Id;

            SeedTeacher("teacher_rep1@eniad.ma", "TeacherPass123!", modAId);
        }

        var client = await CreateTeacherClientAsync("teacher_rep1@eniad.ma", "TeacherPass123!");

        // Try to generate report for other/TOUS
        var responseTous = await client.PostAsJsonAsync("/api/rapports", new
        {
            templateId = "perf-globale",
            format = "PDF",
            filiereCode = "TOUS",
            periode = "S2 2025/2026",
        });
        responseTous.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        var responseOther = await client.PostAsJsonAsync("/api/rapports", new
        {
            templateId = "perf-globale",
            format = "PDF",
            filiereCode = "GE",
            periode = "S2 2025/2026",
        });
        responseOther.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Teacher_cannot_download_another_users_report()
    {
        int modAId;
        int rapportId;
        using (var context = _factory.CreateContext())
        {
            var seeded = SampleData.SeedOne(context);
            modAId = seeded.Module.Id;

            var otherRapport = new Rapport
            {
                TemplateId = "perf-globale",
                Titre = "Admin Report",
                Format = "PDF",
                FiliereCode = "TOUS",
                AuteurId = 999, // Some other user
                AuteurNom = "Admin User",
                Contenu = new byte[] { 1, 2, 3 },
                Taille = 3,
                CreeLe = DateTime.UtcNow,
            };
            context.Rapports.Add(otherRapport);
            context.SaveChanges();
            rapportId = otherRapport.Id;

            SeedTeacher("teacher_rep2@eniad.ma", "TeacherPass123!", modAId);
        }

        var client = await CreateTeacherClientAsync("teacher_rep2@eniad.ma", "TeacherPass123!");

        var response = await client.GetAsync($"/api/rapports/{rapportId}/download");
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    private void SeedTeacher(string email, string password, int moduleId)
    {
        using var ctx = _factory.CreateContext();
        if (!ctx.Utilisateurs.Any(u => u.Email == email))
        {
            ctx.Utilisateurs.Add(new Utilisateur
            {
                Email = email,
                Nom = "Teacher",
                Prenom = "Test",
                MotDePasseHash = BCrypt.Net.BCrypt.HashPassword(password),
                Role = "Enseignant",
                ModuleId = moduleId,
                EstActif = true,
            });
            ctx.SaveChanges();
        }
    }

    private async Task<HttpClient> CreateTeacherClientAsync(string email, string password)
    {
        var client = _factory.CreateClient();
        var token = await AuthHelper.GetTokenAsync(client, email, password);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    private record RapportMeta(int Id, string Format, int Taille);
}
