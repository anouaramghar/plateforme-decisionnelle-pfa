using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using PlateformePFA.API.Data;
using PlateformePFA.API.Models;
using PlateformePFA.API.Services;
using PlateformePFA.Tests.Fixtures;
using Xunit;

namespace PlateformePFA.Tests.Controllers;

/// <summary>
/// A controllable in-memory email sender so send tests can assert success and
/// failure paths deterministically (the real SmtpEmailSender always fails in
/// tests because no SMTP server is configured).
/// </summary>
public sealed class FakeEmailSender : IEmailSender
{
    public bool ShouldSucceed { get; set; } = true;
    public string FailureReason { get; set; } = "SMTP indisponible";
    public List<(string to, string subject, string body)> Sent { get; } = new();

    public void Reset() { ShouldSucceed = true; FailureReason = "SMTP indisponible"; Sent.Clear(); }

    public Task<(bool ok, string? error)> SendAsync(string to, string subject, string body, CancellationToken ct = default)
    {
        if (ShouldSucceed)
        {
            Sent.Add((to, subject, body));
            return Task.FromResult<(bool, string?)>((true, null));
        }
        return Task.FromResult<(bool, string?)>((false, FailureReason));
    }
}

/// <summary>
/// TestWebFactory with the SMTP sender swapped for a controllable fake.
/// </summary>
public class OutreachWebFactory : TestWebFactory
{
    public FakeEmailSender Email { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);
        builder.ConfigureTestServices(services =>
        {
            var d = services.Single(x => x.ServiceType == typeof(IEmailSender));
            services.Remove(d);
            services.AddSingleton<IEmailSender>(Email);
        });
    }
}

/// <summary>
/// Exercises the student-outreach workflow: persisted meeting/draft state,
/// editable drafts, schedule-and-send-once delivery, and meeting outcomes.
/// </summary>
public class StudentOutreachControllerTests : IClassFixture<OutreachWebFactory>
{
    private readonly OutreachWebFactory _factory;

    public StudentOutreachControllerTests(OutreachWebFactory factory)
    {
        _factory = factory;
        _factory.SeedAdmin();
        using var ctx = _factory.CreateContext();
        SampleData.SeedOne(ctx);
        _factory.Email.Reset();
    }

    private async Task<HttpClient> AuthedClientAsync()
    {
        var client = _factory.CreateClient();
        var token = await AuthHelper.GetAdminTokenAsync(client);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    private async Task<HttpClient> TeacherClientAsync()
    {
        using (var ctx = _factory.CreateContext())
        {
            SampleData.SeedOne(ctx);
            var module = ctx.Modules.Single(m => m.Code == "GI01");
            if (!ctx.Utilisateurs.Any(u => u.Email == "teacher@eniad.ma"))
            {
                ctx.Utilisateurs.Add(new Utilisateur
                {
                    Email = "teacher@eniad.ma", Nom = "Teach", Prenom = "Er",
                    MotDePasseHash = BCrypt.Net.BCrypt.HashPassword("TeacherPass!2026"),
                    Role = "Enseignant", EstActif = true, ModuleId = module.Id,
                });
                ctx.SaveChanges();
            }
        }
        var client = _factory.CreateClient();
        var token = await AuthHelper.GetTokenAsync(client, "teacher@eniad.ma", "TeacherPass!2026");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    // Fresh student WITH an email per call (drafts require a recipient address),
    // in the GI/CI1 cohort. Static counter keeps matricules unique across the
    // class's shared InMemory store.
    private static int _studentSeq;
    private int FreshStudentId()
    {
        using var ctx = _factory.CreateContext();
        var fil = ctx.Filieres.First(f => f.Code == "GI");
        var seq = System.Threading.Interlocked.Increment(ref _studentSeq);
        var e = new Etudiant
        {
            Matricule = $"EOUT{seq:D5}", Nom = "Outreach", Prenom = $"Stud{seq}",
            Email = $"stud{seq}@eniad.ma",
            FiliereId = fil.Id, Niveau = "CI1", Annee = "2025/2026",
        };
        ctx.Etudiants.Add(e);
        ctx.SaveChanges();
        return e.Id;
    }

    private record CaseResponse(int Id);

    private async Task<int> CreateCaseAsync(HttpClient client, int etuId)
    {
        var res = await client.PostAsJsonAsync("/api/intervention-cases", new
        {
            etudiantId = etuId, motif = "Outreach", priorite = "High",
        });
        res.StatusCode.Should().Be(HttpStatusCode.Created);
        var body = await res.Content.ReadFromJsonAsync<CaseResponse>();
        return body!.Id;
    }

    // ── Task 1: persistence contract ──────────────────────────────────────────

    [Fact]
    public void Outreach_fields_are_mapped_by_ef()
    {
        using var db = _factory.CreateContext();
        var entity = db.Model.FindEntityType(typeof(InterventionCase))!;
        entity.FindProperty("MeetingScheduledFor").Should().NotBeNull();
        entity.FindProperty("MeetingLocation").Should().NotBeNull();
        entity.FindProperty("MeetingAttendance").Should().NotBeNull();
        entity.FindProperty("MeetingHeldAt").Should().NotBeNull();
    }

    // ── Task 3: draft lifecycle ───────────────────────────────────────────────

    [Fact]
    public async Task Admin_can_create_and_edit_one_draft()
    {
        var client = await AuthedClientAsync();
        var caseId = await CreateCaseAsync(client, FreshStudentId());

        var create = await client.PostAsJsonAsync($"/api/intervention-cases/{caseId}/outreach/draft", new
        {
            subject = "Invitation à un entretien",
            body = "Bonjour, rencontrons-nous pour faire le point."
        });
        create.StatusCode.Should().Be(HttpStatusCode.Created);
        var draft = await create.Content.ReadFromJsonAsync<CaseCommunication>();
        draft!.Status.Should().Be(CommunicationStatus.Draft);

        var edit = await client.PutAsJsonAsync(
            $"/api/intervention-cases/{caseId}/outreach/draft/{draft.Id}",
            new { subject = "Entretien ENIAD", body = "Bonjour, voici la version relue." });
        edit.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // Creating the draft moved the case to InProgress and the edits appear.
        using var verify = _factory.CreateContext();
        verify.InterventionCases.Single(x => x.Id == caseId).Etat.Should().Be(CaseWorkflowState.InProgress);
        var saved = verify.CaseCommunications.Single(x => x.Id == draft.Id);
        saved.Sujet.Should().Be("Entretien ENIAD");
        saved.Corps.Should().Be("Bonjour, voici la version relue.");
        verify.CaseTimelineEvents.Should().Contain(x => x.CaseId == caseId && x.Action == "EmailDraftCreated");
        verify.CaseTimelineEvents.Should().Contain(x => x.CaseId == caseId && x.Action == "EmailDraftEdited");
    }

    [Fact]
    public async Task Second_draft_is_rejected_as_conflict()
    {
        var client = await AuthedClientAsync();
        var caseId = await CreateCaseAsync(client, FreshStudentId());

        var first = await client.PostAsJsonAsync($"/api/intervention-cases/{caseId}/outreach/draft",
            new { subject = "Premier", body = "Bonjour." });
        first.StatusCode.Should().Be(HttpStatusCode.Created);

        var second = await client.PostAsJsonAsync($"/api/intervention-cases/{caseId}/outreach/draft",
            new { subject = "Doublon", body = "Bonjour encore." });
        second.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Enseignant_cannot_create_a_draft()
    {
        var admin = await AuthedClientAsync();
        var caseId = await CreateCaseAsync(admin, FreshStudentId());

        var teacher = await TeacherClientAsync();
        var res = await teacher.PostAsJsonAsync($"/api/intervention-cases/{caseId}/outreach/draft",
            new { subject = "Interdit", body = "Bonjour." });
        res.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }
}
