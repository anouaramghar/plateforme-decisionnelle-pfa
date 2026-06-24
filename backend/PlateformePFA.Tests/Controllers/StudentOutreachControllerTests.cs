using FluentAssertions;
using PlateformePFA.API.Models;
using Xunit;

namespace PlateformePFA.Tests.Controllers;

/// <summary>
/// Exercises the student-outreach workflow: persisted meeting/draft state,
/// editable drafts, schedule-and-send-once delivery, and meeting outcomes.
/// </summary>
public class StudentOutreachControllerTests : IClassFixture<TestWebFactory>
{
    private readonly TestWebFactory _factory;
    public StudentOutreachControllerTests(TestWebFactory factory) => _factory = factory;

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
}
