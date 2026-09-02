using eTicketing.Ticketing.Business.Analytics;
using eTicketing.Ticketing.Business.Analytics.Anomalies;
using eTicketing.Ticketing.Business.Reports;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;

namespace eTicketing.Ticketing.Business.Tests.Analytics;

/// <summary>
/// The detector is tested by injecting a departure into a series whose shape is known, then
/// asserting it comes back — and, just as importantly, that a series with nothing unusual in it
/// comes back empty. A detector that flags everything is as useless as one that flags nothing, and
/// only the second failure is obvious in a demo.
/// </summary>
public class SsaAnomalyDetectorTests
{
    private readonly IAnomalyDetector _sut = new SsaAnomalyDetector(NullLogger<SsaAnomalyDetector>.Instance);

    private static readonly DateOnly Start = new(2026, 6, 1);

    /// <summary>A steady series with a small deterministic wobble — flat enough that an injected
    /// spike is unambiguous, uneven enough that the MAD is not zero.</summary>
    private static List<DailyPoint> Steady(int days, decimal level = 200m) =>
    [
        .. Enumerable.Range(0, days).Select(i =>
        {
            var value = level + (i % 3 * 5m);
            return new DailyPoint(Start.AddDays(i), value, (int)(value / 25m));
        })
    ];

    private static List<DailyPoint> With(List<DailyPoint> series, int index, decimal revenue)
    {
        var copy = series.ToList();
        copy[index] = copy[index] with { Revenue = revenue, Sold = (int)(revenue / 25m) };
        return copy;
    }

    [Fact]
    public void Detect_WithAnInjectedSpike_FlagsThatDayAsASpike()
    {
        var series = With(Steady(60), index: 40, revenue: 2_000m);

        var result = _sut.Detect(series);

        result.Items.Should().Contain(a => a.Date == Start.AddDays(40));
        result.Items.Single(a => a.Date == Start.AddDays(40)).Direction.Should().Be(AnomalyDirection.Spike);
    }

    [Fact]
    public void Detect_WithAnInjectedCollapse_FlagsThatDayAsADrop()
    {
        var series = With(Steady(60), index: 30, revenue: 1m);

        var result = _sut.Detect(series);

        result.Items.Should().Contain(a => a.Date == Start.AddDays(30) && a.Direction == AnomalyDirection.Drop);
    }

    [Fact]
    public void Detect_OnASeriesWithNothingUnusual_ReturnsNothing()
    {
        var result = _sut.Detect(Steady(60));

        result.Items.Should().BeEmpty();
    }

    [Fact]
    public void Detect_BelowTheModelThreshold_UsesTheRobustFallbackAndStillFinds()
    {
        var series = With(Steady(SsaAnomalyDetector.MinPointsForModel - 1), index: 10, revenue: 3_000m);

        var result = _sut.Detect(series);

        result.Source.Should().Be(AnalyticsSource.Heuristic);
        result.Items.Should().Contain(a => a.Date == Start.AddDays(10));
    }

    [Fact]
    public void Detect_WithTooShortASeries_ReportsInsufficientAndFindsNothing()
    {
        var result = _sut.Detect(Steady(SsaAnomalyDetector.MinPointsForHeuristic - 1));

        result.Source.Should().Be(AnalyticsSource.Insufficient);
        result.Items.Should().BeEmpty();
    }

    [Fact]
    public void Detect_WithNoDataAtAll_ReportsInsufficientRatherThanThrowing()
    {
        var result = _sut.Detect([]);

        result.Source.Should().Be(AnalyticsSource.Insufficient);
        result.Items.Should().BeEmpty();
    }

    /// <summary>A noisy year would otherwise produce a wall of rows. The cut keeps the strongest,
    /// which is the whole reason the ranking exists.</summary>
    [Fact]
    public void Detect_WithManyUnusualDays_ReturnsAtMostTheCap()
    {
        var series = Steady(120);
        foreach (var index in new[] { 10, 20, 30, 40, 50, 60, 70, 80 })
            series = With(series, index, 5_000m + (index * 10m));

        var result = _sut.Detect(series);

        result.Items.Should().HaveCountLessThanOrEqualTo(SsaAnomalyDetector.MaxAnomalies);
    }

    [Fact]
    public void Detect_ReturnsItsRowsInChronologicalOrder()
    {
        var series = Steady(120);
        foreach (var index in new[] { 90, 20, 55 })
            series = With(series, index, 4_000m);

        var result = _sut.Detect(series);

        result.Items.Select(a => a.Date).Should().BeInAscendingOrder();
    }

    /// <summary>A stretch on which nothing happened is not news, however far it sits from a
    /// non-zero median — both figures on the card would be zero.</summary>
    [Fact]
    public void Detect_DoesNotFlagDaysWhereNothingWasExpectedAndNothingHappened()
    {
        var quiet = Enumerable.Range(0, 60)
            .Select(i => new DailyPoint(Start.AddDays(i), 0m, 0))
            .ToList();

        var result = _sut.Detect(quiet);

        result.Source.Should().Be(AnalyticsSource.Insufficient);
        result.Items.Should().BeEmpty();
    }

    [Fact]
    public void Detect_ReportsTheExpectedValueAndDeviationForEachFlaggedDay()
    {
        var series = With(Steady(60, level: 200m), index: 40, revenue: 2_000m);

        var anomaly = _sut.Detect(series).Items.Single(a => a.Date == Start.AddDays(40));

        // Expected comes from the rolling median of the neighbours, so it sits at the steady level
        // rather than being dragged up by the spike it is explaining.
        anomaly.ExpectedRevenue.Should().BeInRange(195m, 215m);
        anomaly.Revenue.Should().Be(2_000m);
        anomaly.DeviationPercent.Should().BeGreaterThan(500m);
        anomaly.Confidence.Should().BeInRange(0d, 1d);
        anomaly.Label.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public void Detect_IsDeterministicAcrossRuns()
    {
        var series = With(Steady(90), index: 50, revenue: 3_000m);

        var first = _sut.Detect(series);
        var second = _sut.Detect(series);

        second.Source.Should().Be(first.Source);
        second.Items.Select(a => a.Date).Should().Equal(first.Items.Select(a => a.Date));
    }
}
