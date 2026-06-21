using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using PlateformePFA.Tests.Fixtures;
using PlateformePFA.API.Models;
using Xunit;

namespace PlateformePFA.Tests.Controllers;

public class PredictionsControllerTests : IClassFixture<TestWebFactory>
{
    private readonly TestWebFactory _factory;

    public PredictionsControllerTests(TestWebFactory factory)
    {
        _factory = factory;
        _factory.SeedAdmin();
        using var ctx = _factory.CreateContext();
        SampleData.SeedOne(ctx);
    }

    [Fact]
    public async Task Summary_returns_scatter_and_runs_when_no_ml_predictions()
    {
        var client = _factory.CreateClient();
        var token = await AuthHelper.GetAdminTokenAsync(client);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var res = await client.GetAsync("/api/predictions/summary");
        res.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await res.Content.ReadFromJsonAsync<SummaryResponse>();

        body!.Kpis.Evalues.Should().BeGreaterThan(0);
        body.Scatter.Should().NotBeEmpty();
        body.TopARisque.Should().NotBeEmpty();
        // No PredictionML rows seeded yet → no run rows can be synthesised.
        body.Runs.Should().BeEmpty();
    }

    [Fact]
    public async Task Summary_excludes_withdrawn_students()
    {
        using (var context = _factory.CreateContext())
        {
            var seeded = SampleData.SeedOne(context);
            var withdrawn = new Etudiant
            {
                Matricule = "E-WITHDRAWN-PREDICTION", Nom = "Prediction", Prenom = "Withdrawn",
                FiliereId = seeded.Filiere.Id, Niveau = "CI1", Annee = "2025/2026",
                DesinscritLe = DateTime.UtcNow.AddDays(-1),
            };
            context.Etudiants.Add(withdrawn);
            context.SaveChanges();
            context.Notes.Add(new Note
            {
                EtudiantId = withdrawn.Id, ModuleId = seeded.Module.Id,
                NoteFinal = 4m, Annee = "2025/2026", Semestre = "S2",
            });
            context.SaveChanges();
        }

        var client = _factory.CreateClient();
        var token = await AuthHelper.GetAdminTokenAsync(client);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var response = await client.GetAsync("/api/predictions/summary");
        var body = await response.Content.ReadFromJsonAsync<SummaryResponse>();

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        body!.Kpis.Evalues.Should().Be(1);
    }

    private record SummaryResponse(
        KpisRow Kpis,
        List<object> Scatter,
        List<object> TopARisque,
        List<object> Runs);

    private record KpisRow(int Evalues, int RisqueEleve, int RisqueModere, decimal Auc);
}
