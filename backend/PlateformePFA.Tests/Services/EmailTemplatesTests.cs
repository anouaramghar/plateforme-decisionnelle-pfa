using PlateformePFA.API.Services;
using FluentAssertions;
using Xunit;

namespace PlateformePFA.Tests.Services;

public class EmailTemplatesTests
{
    [Fact]
    public void All_templates_are_present()
    {
        EmailTemplates.All.Keys.Should().BeEquivalentTo(new[]
        {
            "meeting_invitation", "absence_warning", "academic_warning",
            "intervention_followup", "case_resolution", "meeting_outreach",
        });
    }

    [Fact]
    public void RenderMeeting_substitutes_date_time_location()
    {
        var t = EmailTemplates.All["meeting_outreach"];
        var (sujet, corps) = EmailTemplates.RenderMeeting(t, "Sara", "El Amrani", "1 juillet 2026", "10:00", "Salle B12");

        corps.Should().Contain("Sara El Amrani");
        corps.Should().Contain("1 juillet 2026");
        corps.Should().Contain("10:00");
        corps.Should().Contain("Salle B12");
        corps.Should().NotContain("{{");
        sujet.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public void Render_substitutes_placeholders()
    {
        var t = EmailTemplates.All["meeting_invitation"];
        var (sujet, corps) = EmailTemplates.Render(t, "Sara", "El Amrani", "M123");

        corps.Should().Contain("Sara El Amrani");
        corps.Should().NotContain("{{");   // no placeholder left unfilled
        sujet.Should().NotBeNullOrWhiteSpace();
    }
}
