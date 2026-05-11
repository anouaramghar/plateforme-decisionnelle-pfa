using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using PlateformePFA.Tests.Fixtures;
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

    private record SummaryResponse(
        KpisRow Kpis,
        List<object> Scatter,
        List<object> TopARisque,
        List<object> Runs);

    private record KpisRow(int Evalues, int RisqueEleve, int RisqueModere, decimal Auc);
}
