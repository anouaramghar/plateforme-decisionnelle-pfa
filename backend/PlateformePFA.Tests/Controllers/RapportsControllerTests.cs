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

    private record RapportMeta(int Id, string Format, int Taille);
}
