using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using PlateformePFA.Tests.Fixtures;
using PlateformePFA.API.Models;
using PlateformePFA.API.Services;
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

/// <summary>
/// Funnel metrics live in their own class so the 4 students + 4 cases they seed
/// don't pollute DashboardControllerTests' shared InMemory store (the summary
/// tests assert exact deltas and sparkline counts).
/// </summary>
public class DashboardFunnelTests : IClassFixture<TestWebFactory>
{
    private readonly TestWebFactory _factory;

    public DashboardFunnelTests(TestWebFactory factory)
    {
        _factory = factory;
        _factory.SeedAdmin();
        using var ctx = _factory.CreateContext();
        SampleData.SeedOne(ctx);
    }

    [Fact]
    public async Task Interventions_returns_outreach_funnel_metrics()
    {
        // Seed one case per primary outreach state + Sent/Failed communications
        // and a linked alert so the risk-to-email median is computable.
        using (var ctx = _factory.CreateContext())
        {
            var fil = ctx.Filieres.Single(f => f.Code == "GI");
            var students = Enumerable.Range(1, 4).Select(i => new Etudiant
            {
                Matricule = $"E-FUN-{i}", Nom = "Funnel", Prenom = $"S{i}",
                Email = $"fun{i}@eniad.ma",
                FiliereId = fil.Id, Niveau = "CI1", Annee = "2025/2026",
            }).ToList();
            ctx.Etudiants.AddRange(students);
            ctx.SaveChanges();

            var admin = ctx.Utilisateurs.Single(u => u.Email == "test-admin@eniad.ma");
            var openCase = new InterventionCase { EtudiantId = students[0].Id, FiliereId = fil.Id, Motif = "Needs contact", Priorite = "High", Etat = CaseWorkflowState.Open, OwnerId = admin.Id, CreeLe = DateTime.UtcNow };
            var progressCase = new InterventionCase { EtudiantId = students[1].Id, FiliereId = fil.Id, Motif = "Draft prepared", Priorite = "High", Etat = CaseWorkflowState.InProgress, OwnerId = admin.Id, CreeLe = DateTime.UtcNow };
            var waitingCase = new InterventionCase { EtudiantId = students[2].Id, FiliereId = fil.Id, Motif = "Meeting scheduled", Priorite = "High", Etat = CaseWorkflowState.WaitingStudent, OwnerId = admin.Id, MeetingScheduledFor = DateTime.UtcNow.AddDays(2), MeetingLocation = "Salle B12", CreeLe = DateTime.UtcNow };
            var resolvedCase = new InterventionCase { EtudiantId = students[3].Id, FiliereId = fil.Id, Motif = "Meeting held", Priorite = "High", Etat = CaseWorkflowState.Resolved, OwnerId = admin.Id, MeetingAttendance = "Held", MeetingHeldAt = DateTime.UtcNow.AddMinutes(-30), Outcome = "Improved", ResolutionSummary = "OK", CreeLe = DateTime.UtcNow };
            ctx.InterventionCases.AddRange(openCase, progressCase, waitingCase, resolvedCase);
            ctx.SaveChanges();

            ctx.CaseCommunications.AddRange(
                new CaseCommunication { CaseId = progressCase.Id, Destinataire = "fun2@eniad.ma", TemplateId = "student_outreach", Sujet = "Draft", Corps = "Body", Status = CommunicationStatus.Draft },
                new CaseCommunication { CaseId = waitingCase.Id, Destinataire = "fun3@eniad.ma", TemplateId = "student_outreach", Sujet = "Sent", Corps = "Body", Status = CommunicationStatus.Sent, EnvoyeLe = DateTime.UtcNow },
                new CaseCommunication { CaseId = openCase.Id, Destinataire = "fun1@eniad.ma", TemplateId = "academic_warning", Sujet = "Failed", Corps = "Body", Status = "Failed" }
            );
            // Linked alert for risk-to-email: created 5h before the Sent comm.
            ctx.Alertes.Add(new Alerte { EtudiantId = students[2].Id, Type = "RisqueEchec", Niveau = "Eleve", Message = "Risk", Resolue = false, CaseId = waitingCase.Id, CreeLe = DateTime.UtcNow.AddHours(-5) });
            ctx.AuditEntries.Add(new AuditEntry { Action = "DuplicateCasePrevented", EntityType = "InterventionCase", EntityId = openCase.Id, Message = "test", UtilisateurNom = "test" });
            ctx.SaveChanges();
        }

        var client = _factory.CreateClient();
        var token = await AuthHelper.GetAdminTokenAsync(client);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var res = await client.GetAsync("/api/dashboard/interventions");
        res.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await res.Content.ReadFromJsonAsync<JsonElement>();

        body.GetProperty("needsContact").GetInt32().Should().Be(1);
        body.GetProperty("emailPrepared").GetInt32().Should().Be(1);
        body.GetProperty("meetingsScheduled").GetInt32().Should().Be(1);
        body.GetProperty("meetingsHeld").GetInt32().Should().Be(1);
        body.GetProperty("failedEmails").GetInt32().Should().Be(1);
        body.GetProperty("duplicateCasesPrevented").GetInt32().Should().BeGreaterThanOrEqualTo(1);
        body.GetProperty("contactedEligiblePercent").GetDecimal().Should().BeInRange(0, 100);
        body.GetProperty("emailDeliverySuccessPercent").GetDecimal().Should().BeInRange(0, 100);
        body.GetProperty("meetingHeldPercent").GetDecimal().Should().BeInRange(0, 100);
        body.GetProperty("medianHoursRiskToEmail").GetDecimal().Should().BeGreaterThanOrEqualTo(0);
    }
}
