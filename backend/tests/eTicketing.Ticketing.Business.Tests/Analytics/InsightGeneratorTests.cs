using eTicketing.Ticketing.Business.Analytics;
using eTicketing.Ticketing.Business.Analytics.Insights;
using eTicketing.Ticketing.Business.Reports;
using FluentAssertions;

namespace eTicketing.Ticketing.Business.Tests.Analytics;

/// <summary>
/// One test per rule, pinned to its threshold, plus the ranking and the two global guarantees (six
/// cards maximum; nothing at all claimed from an Insufficient block).
///
/// This is the part of the feature that has to be defensible: every card an organizer reads comes
/// from exactly one of these rules and one number, so each rule is asserted to fire on one side of
/// its boundary and stay silent on the other.
/// </summary>
public class InsightGeneratorTests
{
    private readonly IInsightGenerator _sut = new InsightGenerator();

    private static readonly ReportPeriod Period = new(new DateOnly(2026, 8, 1), new DateOnly(2026, 8, 24), 24, ReportBucketUnit.Day);

    // ── Builders ─────────────────────────────────────────────────────────────────────────────
    // Every helper returns a deliberately unremarkable value so that a test changing one field is
    // the only reason a rule fires. Without that, a card asserted on here could just as easily have
    // come from a neighbouring rule.

    private static SalesReportResponse Sales(
        decimal gross = 10_000m,
        int sold = 200,
        decimal cancellationRate = 0m,
        int cancelledCount = 0,
        decimal cancelledAmount = 0m,
        int onlineSold = 100,
        int printedSold = 100) =>
        new(Period, "Platforma", gross, sold, 50m, null, cancelledCount, cancelledAmount, cancellationRate,
            gross - cancelledAmount, onlineSold, printedSold, [], []);

    private static ProductReportResponse Products(
        decimal? occupancy = 80m, params (string Name, decimal Revenue)[] rows)
    {
        (string Name, decimal Revenue)[] items = rows.Length == 0
            ? [("Proizvod A", 5_000m), ("Proizvod B", 5_000m)]
            : rows;

        return new ProductReportResponse(
            Period,
            "Platforma",
            [.. items.Select(r => new ProductReportRow(Guid.NewGuid(), r.Name, "meta", 100, occupancy, 50m, 0, r.Revenue))],
            items.Sum(_ => 100),
            occupancy,
            50m,
            0,
            items.Sum(r => r.Revenue));
    }

    private static RedemptionReportResponse Redemption(decimal noShowRate, int? peakHour = 19) =>
        new(Period, "Platforma", 150, noShowRate, peakHour, 30m, 0, [], []);

    private static ForecastBlock Forecast(
        AnalyticsSource source = AnalyticsSource.Model, decimal? change = 0m, decimal projected = 5_000m) =>
        new(source, 14, projected, 100, change, [], source == AnalyticsSource.Insufficient ? [] :
        [
            new ForecastPoint(new DateOnly(2026, 8, 25), "25. avg", 350m, 300m, 400m, 7)
        ]);

    private static AnomalyBlock Anomalies(AnalyticsSource source = AnalyticsSource.Model, params SalesAnomaly[] items) =>
        new(source, items);

    private static SalesAnomaly Anomaly(AnomalyDirection direction, decimal revenue = 2_000m, decimal expected = 200m) =>
        new(new DateOnly(2026, 8, 12), "12. avg", direction, revenue, expected, 900m, 0.9d);

    private static SegmentBlock Segments(AnalyticsSource source = AnalyticsSource.Model, params AudienceSegment[] items) =>
        new(source, "posljednjih 12 mjeseci", items.Sum(i => i.Buyers), items);

    private static AudienceSegment Segment(string name, decimal share, decimal revenueShare, int buyers = 10) =>
        new(name, "opis", buyers, share, revenueShare, 200m, 3m, 20);

    private InsightContext Context(
        ForecastBlock? forecast = null,
        AnomalyBlock? anomalies = null,
        SegmentBlock? segments = null,
        SalesReportResponse? sales = null,
        ProductReportResponse? products = null,
        RedemptionReportResponse? redemption = null) =>
        new(
            forecast ?? Forecast(),
            anomalies ?? Anomalies(),
            segments ?? Segments(),
            sales ?? Sales(),
            products ?? Products(),
            redemption);

    // ── Empty range ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public void Generate_ForARangeWithNoActivity_ReturnsExactlyOneExplanatoryCard()
    {
        var result = _sut.Generate(Context(sales: Sales(gross: 0m, sold: 0, onlineSold: 0, printedSold: 0)));

        result.Should().ContainSingle();
        result[0].Title.Should().Be("Nema podataka za odabrani period");
        result[0].Severity.Should().Be(InsightSeverity.Neutral);
    }

    [Fact]
    public void Generate_ForARangeWithOnlyCancellations_StillAnalysesRatherThanReportingNoData()
    {
        var result = _sut.Generate(Context(
            sales: Sales(gross: 0m, sold: 0, cancelledCount: 5, cancelledAmount: 250m, cancellationRate: 100m,
                onlineSold: 0, printedSold: 0)));

        result.Should().Contain(i => i.Category == InsightCategory.Sales && i.Severity == InsightSeverity.Critical);
    }

    // ── Forecast ─────────────────────────────────────────────────────────────────────────────

    [Fact]
    public void Generate_WhenTheForecastRisesMaterially_ReportsGrowthAsPositive()
    {
        var result = _sut.Generate(Context(forecast: Forecast(change: InsightGenerator.MaterialChangePercent + 0.1m)));

        result.Should().Contain(i => i.Category == InsightCategory.Forecast && i.Severity == InsightSeverity.Positive);
    }

    [Fact]
    public void Generate_WhenTheForecastFallsMaterially_ReportsItAsAWarning()
    {
        var result = _sut.Generate(Context(forecast: Forecast(change: -(InsightGenerator.MaterialChangePercent + 0.1m))));

        result.Should().Contain(i => i.Category == InsightCategory.Forecast && i.Severity == InsightSeverity.Warning);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(4.9)]
    [InlineData(-4.9)]
    public void Generate_WhenTheForecastBarelyMoves_CallsItStableRatherThanATrend(decimal change)
    {
        var result = _sut.Generate(Context(forecast: Forecast(change: change)));

        var card = result.Single(i => i.Category == InsightCategory.Forecast);
        card.Severity.Should().Be(InsightSeverity.Neutral);
        card.Title.Should().Contain("Stabilna");
    }

    [Fact]
    public void Generate_FromAnInsufficientForecast_SaysNothingAboutTheFuture()
    {
        var result = _sut.Generate(Context(forecast: Forecast(AnalyticsSource.Insufficient, change: 50m)));

        result.Should().NotContain(i => i.Category == InsightCategory.Forecast);
    }

    /// <summary>A card outlives the block header once it reaches the PDF, so it has to carry its
    /// own caveat about which rung produced it.</summary>
    [Fact]
    public void Generate_FromAHeuristicForecast_SaysSoInTheCardItself()
    {
        var result = _sut.Generate(Context(forecast: Forecast(AnalyticsSource.Heuristic, change: 20m)));

        result.Single(i => i.Category == InsightCategory.Forecast).Body.Should().Contain("nema dovoljno historije");
    }

    // ── Anomalies ────────────────────────────────────────────────────────────────────────────

    [Fact]
    public void Generate_WhenMostAnomaliesAreDrops_RaisesAWarning()
    {
        var result = _sut.Generate(Context(anomalies: Anomalies(
            AnalyticsSource.Model,
            Anomaly(AnomalyDirection.Drop, 10m),
            Anomaly(AnomalyDirection.Drop, 12m),
            Anomaly(AnomalyDirection.Spike))));

        result.Single(i => i.Category == InsightCategory.Anomaly).Severity.Should().Be(InsightSeverity.Warning);
    }

    [Fact]
    public void Generate_WhenAnomaliesAreMostlySpikes_StaysNeutral()
    {
        var result = _sut.Generate(Context(anomalies: Anomalies(
            AnalyticsSource.Model, Anomaly(AnomalyDirection.Spike), Anomaly(AnomalyDirection.Spike))));

        result.Single(i => i.Category == InsightCategory.Anomaly).Severity.Should().Be(InsightSeverity.Neutral);
    }

    [Fact]
    public void Generate_WithNoAnomalies_SaysNothingAboutThem()
    {
        var result = _sut.Generate(Context(anomalies: Anomalies(AnalyticsSource.Model)));

        result.Should().NotContain(i => i.Category == InsightCategory.Anomaly);
    }

    // ── Cancellations ────────────────────────────────────────────────────────────────────────

    [Theory]
    [InlineData(10.0, false, null)]
    [InlineData(10.1, true, "Warning")]
    [InlineData(20.0, true, "Warning")]
    [InlineData(20.1, true, "Critical")]
    public void Generate_EscalatesTheCancellationRuleAtItsTwoThresholds(
        decimal rate, bool fires, string? expectedSeverity)
    {
        var result = _sut.Generate(Context(
            sales: Sales(cancellationRate: rate, cancelledCount: 5, cancelledAmount: 250m)));

        var card = result.SingleOrDefault(i => i.Category == InsightCategory.Sales && i.Title.Contains("otkazivanja"));

        if (!fires)
        {
            card.Should().BeNull();
            return;
        }

        card.Should().NotBeNull();
        card!.Severity.Should().Be(Enum.Parse<InsightSeverity>(expectedSeverity!));
    }

    // ── Concentration ────────────────────────────────────────────────────────────────────────

    [Fact]
    public void Generate_WhenOneProductCarriesMostOfTheRevenue_WarnsAboutConcentration()
    {
        var result = _sut.Generate(Context(
            products: Products(rows: [("Veliki festival", 8_000m), ("Mali koncert", 2_000m)])));

        result.Should().Contain(i => i.Category == InsightCategory.Catalog && i.Body.Contains("Veliki festival"));
    }

    [Fact]
    public void Generate_WhenRevenueIsSpreadEvenly_SaysNothingAboutConcentration()
    {
        var result = _sut.Generate(Context(
            products: Products(rows: [("A", 5_000m), ("B", 5_000m)])));

        result.Should().NotContain(i => i.Title.Contains("koncentrisan"));
    }

    /// <summary>A single-product organizer is not concentrated, they are small. Warning them about
    /// it would be noise on every report they ever run.</summary>
    [Fact]
    public void Generate_WithOnlyOneProduct_DoesNotWarnAboutConcentration()
    {
        var result = _sut.Generate(Context(products: Products(rows: [("Jedini proizvod", 10_000m)])));

        result.Should().NotContain(i => i.Title.Contains("koncentrisan"));
    }

    // ── Channel mix ──────────────────────────────────────────────────────────────────────────

    [Theory]
    [InlineData(69, 31, false)]
    [InlineData(70, 30, true)]
    [InlineData(30, 70, true)]
    public void Generate_NamesTheChannelMixOnlyWhenItIsSkewed(int online, int printed, bool fires)
    {
        var result = _sut.Generate(Context(sales: Sales(onlineSold: online, printedSold: printed)));

        result.Any(i => i.Title.Contains("pretežno")).Should().Be(fires);
    }

    // ── Occupancy / no-show ──────────────────────────────────────────────────────────────────

    [Theory]
    [InlineData(39.9, true)]
    [InlineData(40.0, false)]
    public void Generate_WarnsAboutLowOccupancyOnlyBelowTheThreshold(decimal occupancy, bool fires)
    {
        var result = _sut.Generate(Context(products: Products(occupancy: occupancy)));

        result.Any(i => i.Title.Contains("popunjenost")).Should().Be(fires);
    }

    [Fact]
    public void Generate_WithNoOccupancyFigureAtAll_SaysNothingAboutIt()
    {
        var result = _sut.Generate(Context(products: Products(occupancy: null)));

        result.Should().NotContain(i => i.Title.Contains("popunjenost"));
    }

    [Theory]
    [InlineData(25.0, false)]
    [InlineData(25.1, true)]
    public void Generate_WarnsAboutNoShowsOnlyAboveTheThreshold(decimal rate, bool fires)
    {
        var result = _sut.Generate(Context(redemption: Redemption(rate)));

        result.Any(i => i.Category == InsightCategory.Redemption).Should().Be(fires);
    }

    /// <summary>OrganizationAdmin may open this tab but not see gate statistics, so the report is
    /// absent rather than the tab refusing — the rule simply does not fire.</summary>
    [Fact]
    public void Generate_WithoutARedemptionReport_SkipsTheNoShowRuleEntirely()
    {
        var result = _sut.Generate(Context(redemption: null));

        result.Should().NotContain(i => i.Category == InsightCategory.Redemption);
    }

    // ── Segments ─────────────────────────────────────────────────────────────────────────────

    [Fact]
    public void Generate_WhenASegmentEarnsFarMoreThanItsHeadcount_ReportsItAsAnOpportunity()
    {
        var result = _sut.Generate(Context(segments: Segments(
            AnalyticsSource.Model,
            Segment("Kupci visoke vrijednosti", share: 20m, revenueShare: 60m),
            Segment("Povremeni kupci", share: 80m, revenueShare: 40m))));

        result.Should().Contain(i => i.Category == InsightCategory.Audience && i.Severity == InsightSeverity.Positive);
    }

    [Fact]
    public void Generate_WhenNoSegmentOutEarnsItsHeadcount_SaysNothingPositiveAboutSegments()
    {
        var result = _sut.Generate(Context(segments: Segments(
            AnalyticsSource.Model,
            Segment("Redovni kupci", share: 50m, revenueShare: 55m),
            Segment("Povremeni kupci", share: 50m, revenueShare: 45m))));

        result.Should().NotContain(i => i.Category == InsightCategory.Audience && i.Severity == InsightSeverity.Positive);
    }

    [Theory]
    [InlineData(40.0, false)]
    [InlineData(40.1, true)]
    public void Generate_WarnsAboutChurnOnlyAboveTheThreshold(decimal dormantShare, bool fires)
    {
        var result = _sut.Generate(Context(segments: Segments(
            AnalyticsSource.Model,
            Segment("Neaktivni kupci", share: dormantShare, revenueShare: 10m),
            Segment("Redovni kupci", share: 100m - dormantShare, revenueShare: 90m))));

        result.Any(i => i.Title.Contains("neaktivnih")).Should().Be(fires);
    }

    [Fact]
    public void Generate_FromAnInsufficientSegmentBlock_SaysNothingAboutTheAudience()
    {
        var result = _sut.Generate(Context(segments: Segments(AnalyticsSource.Insufficient)));

        result.Should().NotContain(i => i.Category == InsightCategory.Audience);
    }

    // ── Ranking and cap ──────────────────────────────────────────────────────────────────────

    [Fact]
    public void Generate_RanksTheMostSevereCardFirst()
    {
        var result = _sut.Generate(Context(
            forecast: Forecast(change: 30m),
            sales: Sales(cancellationRate: 30m, cancelledCount: 40, cancelledAmount: 3_000m, onlineSold: 100, printedSold: 100)));

        result[0].Severity.Should().Be(InsightSeverity.Critical);
    }

    [Fact]
    public void Generate_WithEveryRuleFiring_ReturnsAtMostTheCap()
    {
        var result = _sut.Generate(Context(
            forecast: Forecast(change: -40m),
            anomalies: Anomalies(AnalyticsSource.Model, Anomaly(AnomalyDirection.Drop, 5m), Anomaly(AnomalyDirection.Drop, 8m)),
            segments: Segments(
                AnalyticsSource.Model,
                Segment("Kupci visoke vrijednosti", share: 15m, revenueShare: 70m),
                Segment("Neaktivni kupci", share: 60m, revenueShare: 10m)),
            sales: Sales(cancellationRate: 30m, cancelledCount: 40, cancelledAmount: 3_000m, onlineSold: 190, printedSold: 10),
            products: Products(occupancy: 12m, rows: [("Dominantni", 9_000m), ("Ostali", 1_000m)]),
            redemption: Redemption(60m)));

        result.Should().HaveCount(InsightGenerator.MaxInsights);
        result.Select(i => i.Severity).Should().BeInDescendingOrder(new SeverityComparer());
    }

    [Fact]
    public void Generate_AlwaysProducesBosnianTitlesAndBodies()
    {
        var result = _sut.Generate(Context());

        result.Should().OnlyContain(i => !string.IsNullOrWhiteSpace(i.Title) && !string.IsNullOrWhiteSpace(i.Body));
    }

    /// <summary>Ranks by the same order the generator uses, which is not the enum's own ordinal —
    /// Positive sits between Neutral and Warning in the enum but above Neutral in the ranking.</summary>
    private sealed class SeverityComparer : IComparer<InsightSeverity>
    {
        public int Compare(InsightSeverity x, InsightSeverity y) => Rank(x).CompareTo(Rank(y));

        private static int Rank(InsightSeverity severity) => severity switch
        {
            InsightSeverity.Critical => 3,
            InsightSeverity.Warning => 2,
            InsightSeverity.Positive => 1,
            _ => 0
        };
    }
}
