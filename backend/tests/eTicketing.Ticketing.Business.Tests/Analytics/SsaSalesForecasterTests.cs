using eTicketing.Ticketing.Business.Analytics;
using eTicketing.Ticketing.Business.Analytics.Forecasting;
using eTicketing.Ticketing.Business.Reports;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;

namespace eTicketing.Ticketing.Business.Tests.Analytics;

/// <summary>
/// The forecaster's contract is the source ladder, not the accuracy of any one prediction — an SSA
/// fit is not something a unit test can assert a number against. What it can and must assert is
/// that the right rung is chosen for the amount of history available, that the shape of the answer
/// is always usable by the clients, and that the two failure modes which would reach the screen as
/// bugs (negative revenue, a horizon shorter than asked for) cannot happen.
/// </summary>
public class SsaSalesForecasterTests
{
    private readonly ISalesForecaster _sut = new SsaSalesForecaster(NullLogger<SsaSalesForecaster>.Instance);

    private static readonly DateOnly Start = new(2026, 6, 1);

    /// <summary>A series with a real weekly rhythm and a mild upward drift — what SSA is for, and
    /// what a flat synthetic series would not exercise.</summary>
    private static List<DailyPoint> Series(int days, Func<int, decimal>? revenue = null)
    {
        revenue ??= i => 100m + (i * 2m) + (Start.AddDays(i).DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday ? 80m : 0m);

        return
        [
            .. Enumerable.Range(0, days).Select(i =>
            {
                var value = revenue(i);
                return new DailyPoint(Start.AddDays(i), value, (int)(value / 25m));
            })
        ];
    }

    /// <summary>Daily horizons — a fortnight and under — are drawn a day at a time, so the point
    /// count is the horizon itself. The longer ones fold; see the bucketing tests below.</summary>
    [Theory]
    [InlineData(7)]
    [InlineData(14)]
    public void Forecast_WithFullSeason_UsesModelAndReturnsExactlyTheRequestedHorizon(int horizon)
    {
        var result = _sut.Forecast(Series(90), horizon);

        result.Source.Should().Be(AnalyticsSource.Model);
        result.Horizon.Should().Be(horizon);
        result.Points.Should().HaveCount(horizon);
    }

    [Fact]
    public void Forecast_AtTheModelThreshold_UsesModel()
    {
        var result = _sut.Forecast(Series(SsaSalesForecaster.MinPointsForModel), 7);

        result.Source.Should().Be(AnalyticsSource.Model);
    }

    [Fact]
    public void Forecast_JustBelowTheModelThreshold_FallsBackToHeuristic()
    {
        var result = _sut.Forecast(Series(SsaSalesForecaster.MinPointsForModel - 1), 7);

        result.Source.Should().Be(AnalyticsSource.Heuristic);
        result.Points.Should().HaveCount(7);
    }

    [Fact]
    public void Forecast_WithTooLittleHistory_ReportsInsufficientAndForecastsNothing()
    {
        var result = _sut.Forecast(Series(SsaSalesForecaster.MinPointsForHeuristic - 1), 14);

        result.Source.Should().Be(AnalyticsSource.Insufficient);
        result.Points.Should().BeEmpty();
        result.Actual.Should().BeEmpty();
        result.ProjectedRevenue.Should().Be(0m);
        result.ChangePercent.Should().BeNull();
    }

    [Fact]
    public void Forecast_WithNoHistoryAtAll_ReportsInsufficientRatherThanThrowing()
    {
        var result = _sut.Forecast([], 7);

        result.Source.Should().Be(AnalyticsSource.Insufficient);
        result.Points.Should().BeEmpty();
    }

    /// <summary>An SSA extrapolation of a falling series goes below zero within a week or two. The
    /// clamp is what stops a "−340,00 KM" bar reaching the chart, so it is asserted on the input
    /// most likely to produce one.</summary>
    [Fact]
    public void Forecast_OnASteeplyDecliningSeries_NeverProjectsNegativeRevenue()
    {
        var declining = Series(60, i => Math.Max(0m, 600m - (i * 10m)));

        var result = _sut.Forecast(declining, 30);

        result.Points.Should().OnlyContain(p => p.Revenue >= 0m);
        result.Points.Should().OnlyContain(p => p.LowerBound >= 0m);
        result.Points.Should().OnlyContain(p => p.Sold >= 0);
        result.ProjectedRevenue.Should().BeGreaterThanOrEqualTo(0m);
    }

    /// <summary>A quiet quarter arrives here as ninety zero-filled days, not as an empty list.
    /// Fitting them would produce a flat zero labelled Model — an authoritative-looking statement
    /// about a period containing no information at all.</summary>
    [Fact]
    public void Forecast_OnAnAllZeroSeries_ReportsInsufficientRatherThanForecastingZero()
    {
        var result = _sut.Forecast(Series(90, _ => 0m), 14);

        result.Source.Should().Be(AnalyticsSource.Insufficient);
        result.Points.Should().BeEmpty();
        result.ProjectedRevenue.Should().Be(0m);
    }

    /// <summary>The other degenerate shape: real money, but identical every single day. SSA can
    /// answer this with a singular matrix, which must land on the heuristic rung rather than a 500
    /// on a report the user is entitled to see.</summary>
    [Fact]
    public void Forecast_OnAPerfectlyConstantSeries_DegradesInsteadOfThrowing()
    {
        var result = _sut.Forecast(Series(90, _ => 250m), 14);

        result.Source.Should().NotBe(AnalyticsSource.Insufficient);
        result.Points.Should().HaveCount(14);
        result.Points.Should().OnlyContain(p => p.Revenue >= 0m);
    }

    [Fact]
    public void Forecast_ContinuesTheSeries_StartingTheDayAfterTheLastActual()
    {
        var history = Series(60);

        var result = _sut.Forecast(history, 7);

        result.Points[0].Date.Should().Be(history[^1].Date.AddDays(1));
        result.Points[^1].Date.Should().Be(history[^1].Date.AddDays(7));
        result.Points.Should().OnlyContain(p => !string.IsNullOrWhiteSpace(p.Label));
    }

    /// <summary>The tail is what lets the client draw one unbroken line, so it has to be the actual
    /// last days of the input and no longer than the horizon.</summary>
    [Fact]
    public void Forecast_ReturnsTheActualTailItContinues()
    {
        var history = Series(60);

        var result = _sut.Forecast(history, 14);

        // Six bars of history at most, whatever a bar covers — enough to see where the line comes
        // from, not so much that half the chart is the past.
        result.Actual.Should().HaveCount(6);
        result.Actual[^1].Date.Should().Be(history[^1].Date);
        result.Actual[^1].Revenue.Should().Be(Math.Round(history[^1].Revenue, 2));
    }

    [Fact]
    public void Forecast_WhenThePrecedingTailSoldNothing_ReportsNoChangeRatherThanInfinity()
    {
        // Real sales for the first six weeks, then a dead fortnight — the comparison baseline is
        // zero and a percentage against it is not a number.
        var history = Series(56).Concat(Series(14, _ => 0m).Select((p, i) =>
            new DailyPoint(Start.AddDays(56 + i), 0m, 0))).ToList();

        var result = _sut.Forecast(history, 14);

        result.ChangePercent.Should().BeNull();
    }

    [Fact]
    public void Forecast_ProducesAConfidenceBandAroundEveryPoint()
    {
        var result = _sut.Forecast(Series(90), 14);

        result.Points.Should().OnlyContain(p => p.LowerBound <= p.Revenue || p.LowerBound == 0m);
        result.Points.Should().OnlyContain(p => p.UpperBound >= p.Revenue);
    }

    // ── Horizons longer than a month ─────────────────────────────────────────────────────────

    /// <summary>A month is drawn as weeks, matching what ReportRange.Unit does with a month on the
    /// Prodaja tab. Thirty labelled daily bars are not more informative than five weekly ones — they
    /// are the same five weeks with twenty-five more labels over them.</summary>
    [Fact]
    public void Forecast_ForAMonthHorizon_DrawsWeeks()
    {
        var result = _sut.Forecast(Series(120), 30);

        result.Points.Should().HaveCount(5);
        result.Points.Select(p => p.Date).Should().BeInAscendingOrder();
    }

    /// <summary>A year of daily bars is 16,000px of chart nobody scrolls through, so the longer
    /// horizons are folded into weeks, fortnights and months — around a dozen bars whichever is
    /// asked for.</summary>
    [Theory]
    [InlineData(90, 13)]
    [InlineData(180, 13)]
    [InlineData(365, 13)]
    public void Forecast_ForALongHorizon_FoldsTheProjectionIntoADrawableNumberOfBars(
        int horizon, int expectedPoints)
    {
        var result = _sut.Forecast(Series(366), horizon);

        result.Points.Should().HaveCount(expectedPoints);
        result.Points.Select(p => p.Date).Should().BeInAscendingOrder();
        result.Points.Should().OnlyContain(p => !string.IsNullOrWhiteSpace(p.Label));
    }

    /// <summary>Folding is a drawing decision, never an arithmetic one: the headline total is the
    /// sum of the projected days and must survive being bucketed.</summary>
    [Theory]
    [InlineData(30)]
    [InlineData(90)]
    [InlineData(180)]
    [InlineData(365)]
    public void Forecast_KeepsTheProjectedTotalEqualToTheSumOfItsPoints(int horizon)
    {
        var result = _sut.Forecast(Series(366), horizon);

        result.Points.Sum(p => p.Revenue).Should().BeApproximately(result.ProjectedRevenue, 0.05m);
        result.Points.Sum(p => p.Sold).Should().Be(result.ProjectedSold);
    }

    /// <summary>However long the horizon, the history in front of it stays a handful of bars —
    /// the card is named after the projection, not after the past.</summary>
    [Theory]
    [InlineData(30)]
    [InlineData(90)]
    [InlineData(180)]
    [InlineData(365)]
    public void Forecast_NeverDrawsMoreThanAHandfulOfActualBars(int horizon)
    {
        var result = _sut.Forecast(Series(366), horizon);

        result.Actual.Should().HaveCountLessThanOrEqualTo(6);
        result.Actual.Should().NotBeEmpty();
    }

    /// <summary>
    /// The tail of actuals is anchored at its last day, not its first.
    ///
    /// The last actual bar sits against the first projected one, so a short bucket there would
    /// invent a slump at exactly the join. The short one belongs at the far, oldest end.
    /// </summary>
    [Fact]
    public void Forecast_AnchorsTheBucketedActualTailAtTheJoinWithTheProjection()
    {
        var history = Series(366);

        var result = _sut.Forecast(history, 365);

        var bucketDays = SsaSalesForecaster.BucketDays(365);
        result.Actual[^1].Date.Should().Be(history[^1].Date.AddDays(-(bucketDays - 1)));
        result.Actual[^1].Revenue.Should().Be(
            Math.Round(history.TakeLast(bucketDays).Sum(p => p.Revenue), 2));
    }

    /// <summary>
    /// A model may not project further than it has seen.
    ///
    /// Asked for a year from a month of history SSA does not fail — it extrapolates the month's
    /// weekly component 365 times and returns a confident straight line with a 95% band around it.
    /// The heuristic rung says the same thing with the caveat attached, which is the honest answer.
    /// </summary>
    [Fact]
    public void Forecast_WhenTheHorizonOutrunsTheHistory_FallsBackToHeuristic()
    {
        var result = _sut.Forecast(Series(30), 365);

        result.Source.Should().Be(AnalyticsSource.Heuristic);
        result.Points.Should().NotBeEmpty();
    }

    [Fact]
    public void Forecast_WhenTheHistoryIsAsLongAsTheHorizon_StillUsesTheModel()
    {
        var result = _sut.Forecast(Series(90), 90);

        result.Source.Should().Be(AnalyticsSource.Model);
    }

    /// <summary>Without an equally-long stretch of actuals behind it there is nothing to compare
    /// against, and a projected year read against a month of sales would print +1.000%.</summary>
    [Fact]
    public void Forecast_WhenTheHorizonOutrunsTheHistory_ReportsNoChangePercent()
    {
        _sut.Forecast(Series(30), 365).ChangePercent.Should().BeNull();
    }

    [Fact]
    public void Forecast_WhenTheHistoryCoversTheHorizon_StillReportsAChangePercent()
    {
        _sut.Forecast(Series(120), 90).ChangePercent.Should().NotBeNull();
    }

    /// <summary>
    /// One freak day must not blow up the confidence band.
    ///
    /// The band used to be built on the standard deviation, which a single sold-out night in an
    /// otherwise quiet month drags up by orders of magnitude — the interval printed under the chart
    /// came out in the tens of millions against a projection of a few hundred thousand. A number
    /// that absurd is not caution; it teaches the reader to ignore the band. The robust spread
    /// ignores the outlier the anomaly block is there to report separately.
    /// </summary>
    [Fact]
    public void Forecast_WithOneFreakDay_KeepsTheConfidenceBandProportionate()
    {
        // Twenty ordinary days around 100 KM, and one night that sold 1.5 million.
        var history = Series(20, i => i == 6 ? 1_500_000m : 80m + (i % 5) * 10m);

        var result = _sut.Forecast(history, 14);

        result.Source.Should().Be(AnalyticsSource.Heuristic);
        result.Points.Sum(p => p.UpperBound).Should().BeLessThan(result.ProjectedRevenue * 5);
    }

    /// <summary>The band still has to exist. A projection presented without uncertainty reads as a
    /// promise, so a robust spread of zero — every day identical — falls back to something rather
    /// than collapsing the interval onto the line.</summary>
    [Fact]
    public void Forecast_OnASeriesWithOneOutlierAndNoOtherVariation_StillProducesABand()
    {
        var history = Series(20, i => i == 6 ? 900_000m : 100m);

        var result = _sut.Forecast(history, 14);

        result.Points.Should().OnlyContain(p => p.UpperBound > p.Revenue);
    }

    /// <summary>Two runs over identical input must agree — the tab is re-fetched on every horizon
    /// change and a projection that wobbles between requests reads as broken.</summary>
    [Fact]
    public void Forecast_IsDeterministicAcrossRuns()
    {
        var history = Series(90);

        var first = _sut.Forecast(history, 14);
        var second = _sut.Forecast(history, 14);

        second.Source.Should().Be(first.Source);
        second.ProjectedRevenue.Should().Be(first.ProjectedRevenue);
        second.Points.Select(p => p.Revenue).Should().Equal(first.Points.Select(p => p.Revenue));
    }
}
