using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using PlateformePFA.Tests.Fixtures;
using Xunit;

namespace PlateformePFA.Tests.Controllers;

public class AlertesControllerTests : IClassFixture<TestWebFactory>
{
    private readonly TestWebFactory _factory;

    public AlertesControllerTests(TestWebFactory factory)
    {
        _factory = factory;
        _factory.SeedAdmin();
        using var ctx = _factory.CreateContext();
        SampleData.SeedOne(ctx);
    }

    [Fact]
    public async Task GetAlertes_returns_paginated_result()
    {
        var client = _factory.CreateClient();
        var token = await AuthHelper.GetAdminTokenAsync(client);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var res = await client.GetAsync("/api/alertes");
        res.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task PostAlerte_creates_alert()
    {
        var client = _factory.CreateClient();
        var token = await AuthHelper.GetAdminTokenAsync(client);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        using var ctx = _factory.CreateContext();
        var etu = ctx.Etudiants.First(e => e.Matricule == "E10001");

        var res = await client.PostAsJsonAsync("/api/alertes", new
        {
            etudiantId = etu.Id,
            type = "RisqueEchec",
            niveau = "Eleve",
            message = "Test alert",
        });

        res.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    [Fact]
    public async Task BatchResolve_resolves_multiple_alerts()
    {
        var client = _factory.CreateClient();
        var token = await AuthHelper.GetAdminTokenAsync(client);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        using var ctx = _factory.CreateContext();
        var etu = ctx.Etudiants.First(e => e.Matricule == "E10001");

        // Create two alerts
        var res1 = await client.PostAsJsonAsync("/api/alertes", new
        {
            etudiantId = etu.Id,
            type = "NoteFaible",
            niveau = "Moyen",
            message = "Alert 1",
        });
        var res2 = await client.PostAsJsonAsync("/api/alertes", new
        {
            etudiantId = etu.Id,
            type = "AbsenceExcessive",
            niveau = "Eleve",
            message = "Alert 2",
        });

        var body1 = await res1.Content.ReadFromJsonAsync<AlertResponse>();
        var body2 = await res2.Content.ReadFromJsonAsync<AlertResponse>();

        // Batch resolve
        var resolveRes = await client.PatchAsJsonAsync("/api/alertes/batch-resolve", new
        {
            ids = new[] { body1!.Id, body2!.Id },
        });

        resolveRes.StatusCode.Should().Be(HttpStatusCode.OK);
        var resolveBody = await resolveRes.Content.ReadFromJsonAsync<BatchResolveResponse>();
        resolveBody!.Resolved.Should().Be(2);
    }

    private record AlertResponse(int Id);
    private record BatchResolveResponse(int Resolved);
}
