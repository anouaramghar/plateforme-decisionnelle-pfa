using FluentAssertions;
using PlateformePFA.API.Helpers;
using Xunit;

namespace PlateformePFA.Tests.Helpers;

public sealed class AcademicPeriodTests
{
    [Theory]
    [InlineData("S1", 1)]
    [InlineData("S5", 5)]
    [InlineData("S9", 9)]
    public void SemesterNumber_accepts_real_eniad_semesters(string value, int expected)
    {
        AcademicPeriod.SemesterNumber(value).Should().Be(expected);
    }

    [Fact]
    public void SemesterDateRange_keeps_s5_inside_the_selected_academic_year()
    {
        var (start, end) = AcademicPeriod.SemesterDateRange("2025/2026", "S5");

        start.Should().Be(new DateTime(2025, 9, 1, 0, 0, 0, DateTimeKind.Utc));
        end.Should().Be(new DateTime(2026, 2, 1, 0, 0, 0, DateTimeKind.Utc));
    }
}
