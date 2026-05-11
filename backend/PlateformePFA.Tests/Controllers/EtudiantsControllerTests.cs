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
    public async Task With_stats_includes_moyenne_and_modules()
    {
        var client = _factory.CreateClient();
        var token = await AuthHelper.GetAdminTokenAsync(client);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var res = await client.GetAsync("/api/etudiants/with-stats");
        res.StatusCode.Should().Be(HttpStatusCode.OK);
        var rows = await res.Content.ReadFromJsonAsync<List<EtudiantWithStatsRow>>();
        rows.Should().NotBeNull();
        rows!.Should().NotBeEmpty();

        var first = rows.First();
        first.Moyenne.Should().BeGreaterThan(0);
        first.ModulesTotal.Should().BeGreaterThan(0);
        first.ScoreRisque.Should().BeInRange(0, 1);
        first.NomComplet.Should().NotBeNullOrEmpty();
        first.FiliereCode.Should().NotBeNullOrEmpty();
    }

    private record EtudiantWithStatsRow(
        int Id, string Matricule, string NomComplet, string FiliereCode,
        string Niveau, decimal Moyenne, int Absences, int ModulesValides,
        int ModulesTotal, decimal ScoreRisque, string Risque, string Statut);
}
