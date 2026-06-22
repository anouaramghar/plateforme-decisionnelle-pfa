using PlateformePFA.API.Services;
using FluentAssertions;
using Xunit;

namespace PlateformePFA.Tests.Services;

public class EmailTemplatesTests
{
    [Fact]
    public void All_five_templates_are_present()
    {
        EmailTemplates.All.Keys.Should().BeEquivalentTo(new[]
        {
            "meeting_invitation", "absence_warning", "academic_warning",
            "intervention_followup", "case_resolution",
        });
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
