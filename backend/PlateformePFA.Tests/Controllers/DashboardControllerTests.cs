using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using PlateformePFA.Tests.Fixtures;
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
