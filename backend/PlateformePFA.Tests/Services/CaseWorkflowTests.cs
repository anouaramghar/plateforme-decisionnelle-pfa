using PlateformePFA.API.Services;
using FluentAssertions;
using Xunit;

namespace PlateformePFA.Tests.Services;

public class CaseWorkflowTests
{
    private static CaseWorkflow.CaseFacts Facts(bool owner = true, bool outcome = true, bool reason = true)
        => new(HasOwner: owner, HasOutcome: outcome, HasReason: reason);

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
        CaseWorkflow.CanTransition("InProgress", "Resolved", Facts(outcome: true)).ok.Should().BeTrue();
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
}
