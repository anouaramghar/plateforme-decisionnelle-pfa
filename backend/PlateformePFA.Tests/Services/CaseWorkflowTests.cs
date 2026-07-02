using PlateformePFA.API.Services;
using FluentAssertions;
using Xunit;

namespace PlateformePFA.Tests.Services;

public class CaseWorkflowTests
{
    private static CaseWorkflow.CaseFacts Facts(
        bool owner = true,
        bool outcome = true,
        bool reason = true,
        bool monitoring = true,
        bool meetingHeld = true)
        => new(
            HasOwner: owner,
            HasOutcome: outcome,
            HasReason: reason,
            MonitoringComplete: monitoring,
            MeetingHeld: meetingHeld);

    [Fact]
    public void Open_to_InProgress_needs_owner()
    {
        CaseWorkflow.CanTransition("Open", "InProgress", Facts(owner: false)).ok.Should().BeFalse();
        CaseWorkflow.CanTransition("Open", "InProgress", Facts(owner: true)).ok.Should().BeTrue();
    }

    [Fact]
    public void Resolve_needs_outcome_and_summary()
    {
        CaseWorkflow.CanTransition("InProgress", "Resolved", Facts(outcome: false)).ok.Should().BeFalse();
        CaseWorkflow.CanTransition("InProgress", "Resolved", Facts(outcome: true, meetingHeld: false)).ok.Should().BeFalse();
        CaseWorkflow.CanTransition("InProgress", "Resolved", Facts(outcome: true)).ok.Should().BeTrue();
    }

    [Theory]
    [InlineData("Monitoring")]
    [InlineData("InProgress")]
    public void Every_path_to_resolved_requires_a_held_meeting(string from)
    {
        CaseWorkflow.CanTransition(from, "Resolved", Facts(meetingHeld: false)).ok.Should().BeFalse();
        CaseWorkflow.CanTransition(from, "Resolved", Facts(meetingHeld: true)).ok.Should().BeTrue();
    }

    [Fact]
    public void Reopen_needs_reason()
    {
        CaseWorkflow.CanTransition("Closed", "InProgress", Facts(reason: false)).ok.Should().BeFalse();
        CaseWorkflow.CanTransition("Closed", "InProgress", Facts(reason: true)).ok.Should().BeTrue();
    }

    [Fact]
    public void Illegal_jumps_are_rejected()
    {
        CaseWorkflow.CanTransition("Open", "Closed", Facts()).ok.Should().BeFalse();
        CaseWorkflow.CanTransition("Open", "Resolved", Facts()).ok.Should().BeFalse();
        CaseWorkflow.CanTransition("Closed", "Resolved", Facts()).ok.Should().BeFalse();
    }

    [Fact]
    public void Close_blocked_until_monitoring_period_finished()
    {
        CaseWorkflow.CanTransition("Resolved", "Closed", Facts(monitoring: false)).ok.Should().BeFalse();
        CaseWorkflow.CanTransition("Resolved", "Closed", Facts(monitoring: true)).ok.Should().BeTrue();
    }

    [Fact]
    public void Escalated_is_no_longer_a_state()
    {
        // Escalation is derived (InterventionCase.EnRetard), not a workflow state.
        CaseWorkflow.CanTransition("InProgress", "Escalated", Facts()).ok.Should().BeFalse();
        CaseWorkflowState.All.Should().NotContain("Escalated");
    }

    [Fact]
    public void EnRetard_is_true_only_for_past_due_active_cases()
    {
        var past = DateTime.UtcNow.AddDays(-1);
        var future = DateTime.UtcNow.AddDays(1);

        new API.Models.InterventionCase { Etat = "InProgress", DueDate = past }.EnRetard.Should().BeTrue();
        new API.Models.InterventionCase { Etat = "InProgress", DueDate = future }.EnRetard.Should().BeFalse();
        new API.Models.InterventionCase { Etat = "InProgress", DueDate = null }.EnRetard.Should().BeFalse();
        new API.Models.InterventionCase { Etat = "Resolved",   DueDate = past }.EnRetard.Should().BeFalse();
        new API.Models.InterventionCase { Etat = "Closed",     DueDate = past }.EnRetard.Should().BeFalse();
    }
}
