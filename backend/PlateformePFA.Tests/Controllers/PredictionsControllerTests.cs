using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
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

    [Fact]
    public async Task Predict_returns_502_on_ml_service_outage()
    {
        // Arrange
        int etudiantId;
        using (var ctx = _factory.CreateContext())
        {
            var seeded = SampleData.SeedOne(ctx);
            etudiantId = seeded.Etudiant.Id;
        }

        var client = CreateMockClient(req =>
        {
            var resp = new HttpResponseMessage(HttpStatusCode.ServiceUnavailable);
            return Task.FromResult(resp);
        });
        var token = await AuthHelper.GetAdminTokenAsync(client);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        // Act
        var res = await client.PostAsync($"/api/predictions/predict/{etudiantId}", null);

        // Assert
        res.StatusCode.Should().Be(HttpStatusCode.BadGateway);

        using (var ctx = _factory.CreateContext())
        {
            var predictions = ctx.PredictionsML.Where(p => p.EtudiantId == etudiantId).ToList();
            predictions.Should().NotBeEmpty();
            var latest = predictions.OrderByDescending(p => p.CreeLe).First();
            latest.Status.Should().Be("MlUnavailable");
            latest.ScoreRisque.Should().BeNull();
            latest.Niveau.Should().BeNull();
        }
    }

    [Fact]
    public async Task Predict_returns_502_on_malformed_response()
    {
        // Arrange
        int etudiantId;
        using (var ctx = _factory.CreateContext())
        {
            var seeded = SampleData.SeedOne(ctx);
            etudiantId = seeded.Etudiant.Id;
        }

        var client = CreateMockClient(req =>
        {
            var resp = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("{}", Encoding.UTF8, "application/json")
            };
            return Task.FromResult(resp);
        });
        var token = await AuthHelper.GetAdminTokenAsync(client);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        // Act
        var res = await client.PostAsync($"/api/predictions/predict/{etudiantId}", null);

        // Assert
        res.StatusCode.Should().Be(HttpStatusCode.BadGateway);

        using (var ctx = _factory.CreateContext())
        {
            var predictions = ctx.PredictionsML.Where(p => p.EtudiantId == etudiantId).ToList();
            predictions.Should().NotBeEmpty();
            var latest = predictions.OrderByDescending(p => p.CreeLe).First();
            latest.Status.Should().Be("InvalidResponse");
            latest.ScoreRisque.Should().BeNull();
            latest.Niveau.Should().BeNull();
        }
    }

    [Fact]
    public async Task Predict_returns_502_on_out_of_bounds_response()
    {
        // Arrange
        int etudiantId;
        using (var ctx = _factory.CreateContext())
        {
            var seeded = SampleData.SeedOne(ctx);
            etudiantId = seeded.Etudiant.Id;
        }

        var client = CreateMockClient(req =>
        {
            var resp = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("{\"probabilite\": 1.5, \"niveau_risque\": \"Faible\"}", Encoding.UTF8, "application/json")
            };
            return Task.FromResult(resp);
        });
        var token = await AuthHelper.GetAdminTokenAsync(client);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        // Act
        var res = await client.PostAsync($"/api/predictions/predict/{etudiantId}", null);

        // Assert
        res.StatusCode.Should().Be(HttpStatusCode.BadGateway);

        using (var ctx = _factory.CreateContext())
        {
            var predictions = ctx.PredictionsML.Where(p => p.EtudiantId == etudiantId).ToList();
            predictions.Should().NotBeEmpty();
            var latest = predictions.OrderByDescending(p => p.CreeLe).First();
            latest.Status.Should().Be("InvalidResponse");
            latest.ScoreRisque.Should().BeNull();
            latest.Niveau.Should().BeNull();
        }
    }

    [Fact]
    public async Task Predict_filters_academic_period()
    {
        // Arrange
        int etudiantId;
        using (var ctx = _factory.CreateContext())
        {
            var seeded = SampleData.SeedOne(ctx);
            etudiantId = seeded.Etudiant.Id;

            var nextModule = new Module
            {
                Code = "GI02", Nom = "Web Dev",
                FiliereId = seeded.Filiere.Id, Niveau = "CI1",
                Coefficient = 3m, Semestre = "S1",
            };
            ctx.Modules.Add(nextModule);
            ctx.SaveChanges();

            ctx.Notes.Add(new Note
            {
                EtudiantId = etudiantId, ModuleId = nextModule.Id,
                NoteFinal = 18m, Annee = "2026/2027", Semestre = "S1",
            });
            ctx.Absences.Add(new Absence
            {
                EtudiantId = etudiantId, ModuleId = nextModule.Id,
                NombreHeures = 10, DateAbsence = new DateTime(2026, 10, 15, 0, 0, 0, DateTimeKind.Utc)
            });
            ctx.SaveChanges();
        }

        string? capturedBody = null;
        var client = CreateMockClient(async req =>
        {
            capturedBody = await req.Content!.ReadAsStringAsync();
            var resp = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("{\"probabilite\": 0.25, \"niveau_risque\": \"Faible\"}", Encoding.UTF8, "application/json")
            };
            return resp;
        });
        var token = await AuthHelper.GetAdminTokenAsync(client);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        // Act
        var res = await client.PostAsync($"/api/predictions/predict/{etudiantId}?annee=2025/2026", null);

        // Assert
        res.StatusCode.Should().Be(HttpStatusCode.Created);
        capturedBody.Should().NotBeNull();

        using (var doc = JsonDocument.Parse(capturedBody!))
        {
            var root = doc.RootElement;
            root.GetProperty("nb_modules").GetInt32().Should().Be(1);
            root.GetProperty("moyenne_generale").GetDecimal().Should().Be(12.2m);
            root.GetProperty("taux_absence").GetDouble().Should().Be(0.0);
        }
    }

    private HttpClient CreateMockClient(Func<HttpRequestMessage, Task<HttpResponseMessage>> mockSend)
    {
        var handler = new MockHttpMessageHandler(mockSend);
        var mockHttpClient = new HttpClient(handler);
        var customFactory = _factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                var descriptor = services.FirstOrDefault(d => d.ServiceType == typeof(IHttpClientFactory));
                if (descriptor != null) services.Remove(descriptor);
                services.AddSingleton<IHttpClientFactory>(new MockHttpClientFactory(mockHttpClient));
            });
        });
        return customFactory.CreateClient();
    }

    public class MockHttpMessageHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, Task<HttpResponseMessage>> _sendAsync;
        public MockHttpMessageHandler(Func<HttpRequestMessage, Task<HttpResponseMessage>> sendAsync)
        {
            _sendAsync = sendAsync;
        }
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            return _sendAsync(request);
        }
    }

    public class MockHttpClientFactory : IHttpClientFactory
    {
        private readonly HttpClient _client;
        public MockHttpClientFactory(HttpClient client)
        {
            _client = client;
        }
        public HttpClient CreateClient(string name) => _client;
    }

    private record SummaryResponse(
        KpisRow Kpis,
        List<object> Scatter,
        List<object> TopARisque,
        List<object> Runs);

    private record KpisRow(int Evalues, int RisqueEleve, int RisqueModere, decimal Auc);
}
