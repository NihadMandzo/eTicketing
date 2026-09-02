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

    [Theory]
    [InlineData(7)]
    [InlineData(14)]
    [InlineData(30)]
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

        result.Actual.Should().HaveCount(14);
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
