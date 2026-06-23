using FluentAssertions;
using PlateformePFA.API.Services;
using Xunit;

namespace PlateformePFA.Tests.Services;

public class RiskScorerTests
{
    [Theory]
    [InlineData(0.85, "Critical")]
    [InlineData(0.65, "High")]
    [InlineData(0.40, "Medium")]
    [InlineData(0.10, "Low")]
    public void RiskToPriorite_maps_score_to_band(double score, string expected)
        => RiskScorer.RiskToPriorite((decimal)score).Should().Be(expected);

    [Fact]
    public void SuggestedPriorite_takes_the_more_severe_of_risk_and_signal()
    {
        // High ML risk escalates even when the raw signal is mild.
        RiskScorer.SuggestedPriorite(0.90m, "Moyen").Should().Be("Critical");

        // A Critique raw signal is never down-ranked by a low risk score.
        RiskScorer.SuggestedPriorite(0.10m, "Critique").Should().Be("Critical");

        // When they agree, that band is kept.
        RiskScorer.SuggestedPriorite(0.40m, "Moyen").Should().Be("Medium");
    }
}
