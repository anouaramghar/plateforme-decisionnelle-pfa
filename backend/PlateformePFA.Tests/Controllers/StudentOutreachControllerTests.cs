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
    public TaskCompletionSource<bool>? Started { get; set; }
    public TaskCompletionSource<bool>? Continue { get; set; }

    public void Reset()
    {
        ShouldSucceed = true;
        FailureReason = "SMTP indisponible";
        Sent.Clear();
        Started = null;
        Continue = null;
    }

    public async Task<(bool ok, string? error)> SendAsync(string to, string subject, string body, CancellationToken ct = default)
    {
        Started?.TrySetResult(true);
        if (Continue != null) await Continue.Task.WaitAsync(ct);
        if (ShouldSucceed)
        {
            Sent.Add((to, subject, body));
            return (true, null);
        }
        return (false, FailureReason);
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
        db.Model.FindEntityType(typeof(CaseCommunication))!
            .FindProperty("ConcurrencyToken")!.IsConcurrencyToken.Should().BeTrue();
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

    // ── Task 4: schedule and send exactly once ────────────────────────────────

    private async Task<(int caseId, int commId)> CreateDraftAsync(HttpClient client)
    {
        var caseId = await CreateCaseAsync(client, FreshStudentId());
        var res = await client.PostAsJsonAsync($"/api/intervention-cases/{caseId}/outreach/draft",
            new { subject = "Invitation", body = "Bonjour, rendez-vous à l'ENIAD." });
        res.StatusCode.Should().Be(HttpStatusCode.Created);
        var draft = await res.Content.ReadFromJsonAsync<CaseCommunication>();
        return (caseId, draft!.Id);
    }

    [Fact]
    public async Task Schedule_and_send_delivers_and_advances_to_waiting_student()
    {
        var client = await AuthedClientAsync();
        var (caseId, commId) = await CreateDraftAsync(client);
        var requested = DateTime.UtcNow.AddDays(2);

        var res = await client.PostAsJsonAsync(
            $"/api/intervention-cases/{caseId}/outreach/draft/{commId}/send",
            new { scheduledFor = requested, location = "Salle B12" });
        res.StatusCode.Should().Be(HttpStatusCode.NoContent);

        using var verify = _factory.CreateContext();
        var saved = verify.InterventionCases.Single(x => x.Id == caseId);
        saved.MeetingScheduledFor.Should().BeCloseTo(requested, TimeSpan.FromSeconds(1));
        saved.MeetingLocation.Should().Be("Salle B12");
        saved.Etat.Should().Be(CaseWorkflowState.WaitingStudent);
        var comm = verify.CaseCommunications.Single(x => x.Id == commId);
        comm.Status.Should().Be(CommunicationStatus.Sent);
        var timeline = verify.CaseTimelineEvents.Where(x => x.CaseId == caseId).ToList();
        timeline.Should().Contain(x => x.Action == "MeetingScheduled");
        timeline.Should().Contain(x => x.Action == "EmailSent");
        _factory.Email.Sent.Should().ContainSingle();
    }

    [Fact]
    public async Task Send_rejects_a_past_date()
    {
        var client = await AuthedClientAsync();
        var (caseId, commId) = await CreateDraftAsync(client);

        var res = await client.PostAsJsonAsync(
            $"/api/intervention-cases/{caseId}/outreach/draft/{commId}/send",
            new { scheduledFor = DateTime.UtcNow.AddDays(-1), location = "Salle B12" });
        res.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Send_rejects_blank_location()
    {
        var client = await AuthedClientAsync();
        var (caseId, commId) = await CreateDraftAsync(client);

        var res = await client.PostAsJsonAsync(
            $"/api/intervention-cases/{caseId}/outreach/draft/{commId}/send",
            new { scheduledFor = DateTime.UtcNow.AddDays(2), location = "   " });
        res.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Failed_send_keeps_draft_then_retry_succeeds()
    {
        var client = await AuthedClientAsync();
        var (caseId, commId) = await CreateDraftAsync(client);

        _factory.Email.ShouldSucceed = false;
        var send = await client.PostAsJsonAsync(
            $"/api/intervention-cases/{caseId}/outreach/draft/{commId}/send",
            new { scheduledFor = DateTime.UtcNow.AddDays(2), location = "Salle B12" });
        send.StatusCode.Should().Be(HttpStatusCode.NoContent);

        using (var verify = _factory.CreateContext())
        {
            verify.InterventionCases.Single(x => x.Id == caseId).Etat.Should().Be(CaseWorkflowState.InProgress);
            var comm = verify.CaseCommunications.Single(x => x.Id == commId);
            comm.Status.Should().Be(CommunicationStatus.Failed);
            comm.Sujet.Should().Be("Invitation"); // stored text unchanged
            comm.Corps.Should().Be("Bonjour, rendez-vous à l'ENIAD.");
        }

        _factory.Email.ShouldSucceed = true;
        var retry = await client.PostAsync(
            $"/api/intervention-cases/{caseId}/outreach/draft/{commId}/retry", null);
        retry.StatusCode.Should().Be(HttpStatusCode.NoContent);

        using var after = _factory.CreateContext();
        after.InterventionCases.Single(x => x.Id == caseId).Etat.Should().Be(CaseWorkflowState.WaitingStudent);
        after.CaseCommunications.Single(x => x.Id == commId).Status.Should().Be(CommunicationStatus.Sent);
    }

    [Fact]
    public async Task A_sent_email_cannot_be_resent_or_retried()
    {
        var client = await AuthedClientAsync();
        var (caseId, commId) = await CreateDraftAsync(client);

        (await client.PostAsJsonAsync(
            $"/api/intervention-cases/{caseId}/outreach/draft/{commId}/send",
            new { scheduledFor = DateTime.UtcNow.AddDays(2), location = "Salle B12" }))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);

        // Second send: rejected (comm Sent, case already WaitingStudent).
        (await client.PostAsJsonAsync(
            $"/api/intervention-cases/{caseId}/outreach/draft/{commId}/send",
            new { scheduledFor = DateTime.UtcNow.AddDays(3), location = "Salle C1" }))
            .StatusCode.Should().Be(HttpStatusCode.BadRequest);

        // Retry: only Failed is retryable.
        (await client.PostAsync(
            $"/api/intervention-cases/{caseId}/outreach/draft/{commId}/retry", null))
            .StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Concurrent_send_requests_deliver_only_once_and_persist_schedule_before_smtp()
    {
        var client = await AuthedClientAsync();
        var (caseId, commId) = await CreateDraftAsync(client);
        var requested = DateTime.UtcNow.AddDays(2);
        _factory.Email.Started = new(TaskCreationOptions.RunContinuationsAsynchronously);
        _factory.Email.Continue = new(TaskCreationOptions.RunContinuationsAsynchronously);

        var first = client.PostAsJsonAsync(
            $"/api/intervention-cases/{caseId}/outreach/draft/{commId}/send",
            new { scheduledFor = requested, location = "Salle B12" });
        await _factory.Email.Started.Task.WaitAsync(TimeSpan.FromSeconds(5));

        using (var duringSend = _factory.CreateContext())
        {
            duringSend.CaseCommunications.Single(c => c.Id == commId).Status.Should().Be(CommunicationStatus.Queued);
            var intervention = duringSend.InterventionCases.Single(c => c.Id == caseId);
            intervention.MeetingScheduledFor.Should().BeCloseTo(requested, TimeSpan.FromSeconds(1));
            intervention.MeetingLocation.Should().Be("Salle B12");
            duringSend.CaseTimelineEvents.Should().Contain(e => e.CaseId == caseId && e.Action == "MeetingScheduled");
        }

        var second = await client.PostAsJsonAsync(
            $"/api/intervention-cases/{caseId}/outreach/draft/{commId}/send",
            new { scheduledFor = requested.AddDays(1), location = "Salle C1" });
        second.StatusCode.Should().BeOneOf(HttpStatusCode.BadRequest, HttpStatusCode.Conflict);

        _factory.Email.Continue.TrySetResult(true);
        (await first).StatusCode.Should().Be(HttpStatusCode.NoContent);
        _factory.Email.Sent.Should().ContainSingle();
    }

    // ── Task 5: record meeting attendance ─────────────────────────────────────

    private async Task<int> CreateScheduledOutreachAsync(HttpClient client)
    {
        var (caseId, commId) = await CreateDraftAsync(client);
        var send = await client.PostAsJsonAsync(
            $"/api/intervention-cases/{caseId}/outreach/draft/{commId}/send",
            new { scheduledFor = DateTime.UtcNow.AddDays(2), location = "Salle B12" });
        send.StatusCode.Should().Be(HttpStatusCode.NoContent);
        return caseId;
    }

    [Theory]
    [InlineData("Absent")]
    [InlineData("Cancelled")]
    public async Task Non_held_meeting_stays_active(string attendance)
    {
        var client = await AuthedClientAsync();
        var caseId = await CreateScheduledOutreachAsync(client);

        var response = await client.PostAsJsonAsync(
            $"/api/intervention-cases/{caseId}/outreach/meeting-result",
            new { attendance });
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        using var verify = _factory.CreateContext();
        var saved = verify.InterventionCases.Single(x => x.Id == caseId);
        saved.Etat.Should().Be(CaseWorkflowState.WaitingStudent);
        saved.MeetingAttendance.Should().Be(attendance);
        saved.MeetingHeldAt.Should().BeNull();
    }

    [Theory]
    [InlineData("Absent")]
    [InlineData("Cancelled")]
    public async Task Non_held_meeting_can_be_rescheduled(string attendance)
    {
        var client = await AuthedClientAsync();
        var caseId = await CreateScheduledOutreachAsync(client);
        (await client.PostAsJsonAsync(
            $"/api/intervention-cases/{caseId}/outreach/meeting-result",
            new { attendance })).StatusCode.Should().Be(HttpStatusCode.NoContent);

        var draftResponse = await client.PostAsJsonAsync(
            $"/api/intervention-cases/{caseId}/outreach/draft",
            new { subject = "Nouveau rendez-vous", body = "Bonjour, replanifions notre entretien." });
        draftResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        var draft = await draftResponse.Content.ReadFromJsonAsync<CaseCommunication>();

        using (var afterDraft = _factory.CreateContext())
        {
            var intervention = afterDraft.InterventionCases.Single(c => c.Id == caseId);
            intervention.Etat.Should().Be(CaseWorkflowState.InProgress);
            intervention.MeetingAttendance.Should().BeNull();
            intervention.MeetingScheduledFor.Should().BeNull();
        }

        var send = await client.PostAsJsonAsync(
            $"/api/intervention-cases/{caseId}/outreach/draft/{draft!.Id}/send",
            new { scheduledFor = DateTime.UtcNow.AddDays(4), location = "Salle C1" });
        send.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task Held_meeting_requires_outcome_and_resolves()
    {
        var client = await AuthedClientAsync();
        var caseId = await CreateScheduledOutreachAsync(client);
        var heldAt = DateTime.UtcNow.AddMinutes(-10);

        var invalid = await client.PostAsJsonAsync(
            $"/api/intervention-cases/{caseId}/outreach/meeting-result",
            new { attendance = "Held", heldAt, outcome = "Improved", summary = "" });
        invalid.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var valid = await client.PostAsJsonAsync(
            $"/api/intervention-cases/{caseId}/outreach/meeting-result",
            new { attendance = "Held", heldAt, outcome = "Improved", summary = "Entretien réalisé." });
        valid.StatusCode.Should().Be(HttpStatusCode.NoContent);

        using var verify = _factory.CreateContext();
        var saved = verify.InterventionCases.Single(x => x.Id == caseId);
        saved.Etat.Should().Be(CaseWorkflowState.Resolved);
        saved.MeetingAttendance.Should().Be("Held");
        saved.ResolutionSummary.Should().Be("Entretien réalisé.");

        // Audit attribution: the actor is recorded on the timeline event.
        var held = verify.CaseTimelineEvents.Single(x => x.CaseId == caseId && x.Action == "MeetingHeld");
        held.UtilisateurId.Should().HaveValue();
        held.UtilisateurNom.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task Enseignant_cannot_record_a_meeting_result()
    {
        var admin = await AuthedClientAsync();
        var caseId = await CreateScheduledOutreachAsync(admin);

        var teacher = await TeacherClientAsync();
        var res = await teacher.PostAsJsonAsync(
            $"/api/intervention-cases/{caseId}/outreach/meeting-result",
            new { attendance = "Absent" });
        res.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }
}
