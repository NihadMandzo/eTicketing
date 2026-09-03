using eTicketing.Ticketing.Business.Analytics;
using FluentAssertions;

namespace eTicketing.Ticketing.Business.Tests.Analytics;

/// <summary>
/// The horizon's wording. Trivial-looking, and worth pinning: this string is what the findings, the
/// LLM prompt and the PDF all say out loud, and the four horizons are months while the value on the
/// wire is a day count. Written from the raw number the tab would tell an organizer that sales are
/// falling "u narednih 365 dana".
/// </summary>
public class HorizonLabelTests
{
    [Theory]
    [InlineData(30, "mjesec dana")]
    [InlineData(90, "3 mjeseca")]
    [InlineData(180, "6 mjeseci")]
    [InlineData(365, "godinu dana")]
    public void Phrase_ForAnOfferedHorizon_ReadsAsMonths(int days, string expected)
    {
        HorizonLabel.Phrase(days).Should().Be(expected);
    }

    [Fact]
    public void Next_AndPrevious_ProduceTheSentenceFragmentsTheFindingsUse()
    {
        HorizonLabel.Next(180).Should().Be("narednih 6 mjeseci");
        HorizonLabel.Previous(180).Should().Be("prethodnih 6 mjeseci");
    }

    /// <summary>Every horizon the validator lets through has a written form of its own — a horizon
    /// that fell through to the day-count fallback would be a wording bug on a live screen.</summary>
    [Fact]
    public void Phrase_CoversEveryHorizonTheApiAccepts()
    {
        foreach (var horizon in Business.Analytics.Validators.InsightsQueryValidator.AllowedHorizons)
        {
            HorizonLabel.Phrase(horizon).Should().NotBe(
                $"{horizon} dana",
                "horizon {0} fell through to the raw day-count fallback",
                horizon);
        }
    }

    /// <summary>A horizon named inside the service rather than requested through the API — the PDF
    /// export's own constant, or a test — still has to read as Bosnian.</summary>
    [Fact]
    public void Phrase_ForAnUnlistedHorizon_FallsBackToDays()
    {
        HorizonLabel.Next(45).Should().Be("narednih 45 dana");
    }
}
