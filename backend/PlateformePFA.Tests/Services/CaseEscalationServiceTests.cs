using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using PlateformePFA.API.Models;
using PlateformePFA.API.Services;
using PlateformePFA.Tests.Fixtures;
using Xunit;

namespace PlateformePFA.Tests.Services;

public class CaseEscalationServiceTests : IClassFixture<TestWebFactory>
{
    private readonly TestWebFactory _factory;

    public CaseEscalationServiceTests(TestWebFactory factory)
    {
        _factory = factory;
        _factory.SeedAdmin();
        using var ctx = _factory.CreateContext();
        SampleData.SeedOne(ctx);
    }

    [Fact]
    public async Task Sweep_escalates_critical_overdue_and_leaves_others()
    {
        int criticalId, normalId, adminId;
        using (var ctx = _factory.CreateContext())
        {
            var etu = ctx.Etudiants.First(e => e.Matricule == "E10001");
            adminId = ctx.Utilisateurs.Single(u => u.Email == "test-admin@eniad.ma").Id;

            var critical = new InterventionCase
            {
                EtudiantId = etu.Id, FiliereId = etu.FiliereId, Motif = "crit",
                Priorite = "Critical", Etat = CaseWorkflowState.InProgress,
                OwnerId = null, DueDate = DateTime.UtcNow.AddDays(-2),
            };
            var normal = new InterventionCase
            {
                EtudiantId = etu.Id, FiliereId = etu.FiliereId, Motif = "norm",
                Priorite = "High", Etat = CaseWorkflowState.InProgress,
                DueDate = DateTime.UtcNow.AddDays(-2),
            };
            ctx.InterventionCases.AddRange(critical, normal);
            ctx.SaveChanges();
            criticalId = critical.Id; normalId = normal.Id;
        }

        using (var scope = _factory.Services.CreateScope())
        {
            var svc = scope.ServiceProvider.GetRequiredService<CaseEscalationService>();
            var count = await svc.SweepAsync(DateTime.UtcNow);
            count.Should().Be(1);
        }

        using var check = _factory.CreateContext();
        var crit = check.InterventionCases.Single(c => c.Id == criticalId);
        crit.Etat.Should().Be(CaseWorkflowState.Escalated);
        crit.EscaladeLe.Should().NotBeNull();
        crit.OwnerId.Should().Be(adminId);
        check.CaseTimelineEvents.Any(t => t.CaseId == criticalId && t.Action == "Escalated").Should().BeTrue();

        check.InterventionCases.Single(c => c.Id == normalId).Etat.Should().Be(CaseWorkflowState.InProgress);
    }
}