using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using PlateformePFA.Tests.Fixtures;
using PlateformePFA.API.Models;
using Xunit;

namespace PlateformePFA.Tests.Controllers;

public class DashboardControllerTests : IClassFixture<TestWebFactory>
{
    private readonly TestWebFactory _factory;

    public DashboardControllerTests(TestWebFactory factory)
    {
        _factory = factory;
        _factory.SeedAdmin();
        using var ctx = _factory.CreateContext();
        SampleData.SeedOne(ctx);
    }

    [Fact]
    public async Task Summary_returns_kpis_and_breakdowns()
    {
        var client = _factory.CreateClient();
        var token = await AuthHelper.GetAdminTokenAsync(client);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var res = await client.GetAsync("/api/dashboard/summary");
        res.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await res.Content.ReadFromJsonAsync<SummaryResponse>();

        body.Should().NotBeNull();
        body!.Kpis.NbEtudiants.Should().BeGreaterThan(0);
        body.Kpis.MoyGlobale.Should().BeGreaterThan(0);
        body.Kpis.CompareLabel.Should().StartWith("vs ");
        body.NotesByFiliere.Should().NotBeEmpty();
        body.RiskBreakdown.Should().HaveCount(3);
        body.AbsenceTrend.Should().HaveCount(14);
        body.EtudiantsParSemaine.Should().HaveCount(14);
    }

    [Fact]
    public async Task Summary_calculates_population_at_historical_cutoffs()
    {
        var now = DateTime.UtcNow;
        using (var context = _factory.CreateContext())
        {
            var filiere = context.Filieres.Single(f => f.Code == "GI");
            context.Etudiants.AddRange(
                new Etudiant
                {
                    Matricule = "E-HISTORY-ACTIVE", Nom = "History", Prenom = "Active",
                    FiliereId = filiere.Id, Niveau = "CI1", Annee = "2025/2026",
                    CreeLe = now.AddDays(-120),
                },
                new Etudiant
                {
                    Matricule = "E-HISTORY-WITHDRAWN", Nom = "History", Prenom = "Withdrawn",
                    FiliereId = filiere.Id, Niveau = "CI1", Annee = "2025/2026",
                    CreeLe = now.AddDays(-120), DesinscritLe = now.AddDays(-1),
                });
            context.SaveChanges();
        }

        var client = _factory.CreateClient();
        var token = await AuthHelper.GetAdminTokenAsync(client);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var response = await client.GetAsync("/api/dashboard/summary");
        var body = await response.Content.ReadFromJsonAsync<SummaryResponse>();

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        body!.Kpis.NbEtudiantsDelta.Should().Be(0m);
        body.EtudiantsParSemaine.First().Should().Be(2);
    }

    [Fact]
    public async Task Summary_excludes_alerts_for_withdrawn_students()
    {
        using (var context = _factory.CreateContext())
        {
            var filiere = context.Filieres.Single(f => f.Code == "GI");
            var withdrawn = new Etudiant
            {
                Matricule = "E-WITHDRAWN-ALERT", Nom = "Alert", Prenom = "Withdrawn",
                FiliereId = filiere.Id, Niveau = "CI1", Annee = "2025/2026",
                DesinscritLe = DateTime.UtcNow.AddDays(-1),
            };
            context.Etudiants.Add(withdrawn);
            context.SaveChanges();
            context.Alertes.Add(new Alerte
            {
                EtudiantId = withdrawn.Id, Type = "RisqueEchec", Niveau = "Eleve",
                Message = "Should not be operational", Resolue = false,
            });
            context.SaveChanges();
        }

        var client = _factory.CreateClient();
        var token = await AuthHelper.GetAdminTokenAsync(client);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var response = await client.GetAsync("/api/dashboard/summary");
        var body = await response.Content.ReadFromJsonAsync<SummaryResponse>();

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        body!.Kpis.AlertesActives.Should().Be(0);
    }

    private record SummaryResponse(
        KpisRow Kpis,
        List<object> NotesByFiliere,
        List<object> RiskBreakdown,
        List<object> AbsenceTrend,
        List<object> TopARisque,
        List<int> EtudiantsParSemaine,
        int NouveauxCetteSemaine,
        int RetraitsCetteSemaine);

    private record KpisRow(
        int NbEtudiants,
        decimal MoyGlobale,
        decimal TauxReussite,
        int AlertesActives,
        decimal NbEtudiantsDelta,
        decimal MoyGlobaleDelta,
        decimal TauxReussiteDelta,
        int AlertesActivesDelta,
        string CompareLabel);
}
