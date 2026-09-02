using eTicketing.Ticketing.Business.Analytics;
using eTicketing.Ticketing.Business.Analytics.Segmentation;
using eTicketing.Ticketing.Data.Repositories;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;

namespace eTicketing.Ticketing.Business.Tests.Analytics;

/// <summary>
/// Clustering cannot be asserted against a fixed partition — which buyer lands in which group is
/// the model's business. What must hold regardless is that the shares add up, that the names are
/// stable across runs (the failure that would silently relabel every segment between two page
/// loads), and that the ladder drops to fixed tiers when there are too few buyers for the groups to
/// mean anything.
/// </summary>
public class KMeansAudienceSegmenterTests
{
    private readonly IAudienceSegmenter _sut = new KMeansAudienceSegmenter(NullLogger<KMeansAudienceSegmenter>.Instance);

    private static readonly DateOnly AsOf = new(2026, 8, 24);
    private const string Window = "posljednjih 12 mjeseci";

    private static BuyerFacts Buyer(int orders, int tickets, decimal spend, int daysAgo) =>
        new(
            Guid.NewGuid(),
            orders,
            tickets,
            spend,
            AsOf.AddDays(-daysAgo - 30).ToDateTime(TimeOnly.MinValue),
            AsOf.AddDays(-daysAgo).ToDateTime(TimeOnly.MinValue));

    /// <summary>Three obviously different populations plus a dormant tail — enough separation that
    /// any correct clusterer finds structure, and enough buyers to clear the model threshold.</summary>
    private static List<BuyerFacts> MixedAudience()
    {
        var buyers = new List<BuyerFacts>();
        for (var i = 0; i < 8; i++) buyers.Add(Buyer(orders: 6, tickets: 14, spend: 900m + i, daysAgo: 5));
        for (var i = 0; i < 10; i++) buyers.Add(Buyer(orders: 3, tickets: 5, spend: 250m + i, daysAgo: 20));
        for (var i = 0; i < 12; i++) buyers.Add(Buyer(orders: 1, tickets: 1, spend: 40m + i, daysAgo: 60));
        for (var i = 0; i < 6; i++) buyers.Add(Buyer(orders: 1, tickets: 2, spend: 70m + i, daysAgo: 300));
        return buyers;
    }

    [Fact]
    public void Segment_WithAMixedAudience_UsesTheModelAndReturnsPopulatedSegments()
    {
        var result = _sut.Segment(MixedAudience(), AsOf, Window);

        result.Source.Should().Be(AnalyticsSource.Model);
        result.TotalBuyers.Should().Be(36);
        result.Items.Should().NotBeEmpty();
        result.Items.Should().OnlyContain(s => s.Buyers > 0);
        result.WindowLabel.Should().Be(Window);
    }

    [Fact]
    public void Segment_SharesSumToOneHundredPercent()
    {
        var result = _sut.Segment(MixedAudience(), AsOf, Window);

        result.Items.Sum(s => s.SharePercent).Should().BeApproximately(100m, 0.5m);
        result.Items.Sum(s => s.RevenueSharePercent).Should().BeApproximately(100m, 0.5m);
        result.Items.Sum(s => s.Buyers).Should().Be(result.TotalBuyers);
    }

    /// <summary>The single most important property here. K-Means numbers its clusters arbitrarily,
    /// so naming by index would rename every segment on a rerun over unchanged data.</summary>
    [Fact]
    public void Segment_ProducesTheSameNamesAndSharesOnARerun()
    {
        var buyers = MixedAudience();

        var first = _sut.Segment(buyers, AsOf, Window);
        var second = _sut.Segment(buyers, AsOf, Window);

        second.Items.Select(s => s.Name).Should().Equal(first.Items.Select(s => s.Name));
        second.Items.Select(s => s.Buyers).Should().Equal(first.Items.Select(s => s.Buyers));
    }

    [Fact]
    public void Segment_RanksSegmentsSoTheHighestSpendingGroupComesFirst()
    {
        var result = _sut.Segment(MixedAudience(), AsOf, Window);

        result.Items.Select(s => s.AverageSpend).Should().BeInDescendingOrder();
        result.Items[0].Name.Should().Be("Kupci visoke vrijednosti");
    }

    [Fact]
    public void Segment_JustBelowTheModelThreshold_FallsBackToFixedTiers()
    {
        var buyers = Enumerable.Range(0, KMeansAudienceSegmenter.MinBuyersForModel - 1)
            .Select(i => Buyer(orders: 1 + (i % 3), tickets: 2, spend: 50m + (i * 20m), daysAgo: i * 10))
            .ToList();

        var result = _sut.Segment(buyers, AsOf, Window);

        result.Source.Should().Be(AnalyticsSource.Heuristic);
        result.Items.Should().NotBeEmpty();
        result.Items.Sum(s => s.Buyers).Should().Be(buyers.Count);
    }

    [Fact]
    public void Segment_WithNoBuyers_ReportsInsufficient()
    {
        var result = _sut.Segment([], AsOf, Window);

        result.Source.Should().Be(AnalyticsSource.Insufficient);
        result.Items.Should().BeEmpty();
        result.TotalBuyers.Should().Be(0);
    }

    /// <summary>Every buyer identical means every feature column has zero variance, which is what a
    /// freshly seeded demo database looks like. The clusterer has nothing to separate on and must
    /// degrade rather than throw.</summary>
    [Fact]
    public void Segment_WhenEveryBuyerIsIdentical_DegradesInsteadOfThrowing()
    {
        var buyers = Enumerable.Range(0, 40).Select(_ => Buyer(1, 1, 100m, 10)).ToList();

        var result = _sut.Segment(buyers, AsOf, Window);

        result.Source.Should().NotBe(AnalyticsSource.Insufficient);
        result.Items.Sum(s => s.Buyers).Should().Be(40);
    }

    [Fact]
    public void Segment_InTierMode_PutsLongDormantBuyersInTheDormantSegment()
    {
        var buyers = new List<BuyerFacts>
        {
            Buyer(orders: 1, tickets: 1, spend: 60m, daysAgo: 400),
            Buyer(orders: 1, tickets: 1, spend: 60m, daysAgo: 365),
            Buyer(orders: 3, tickets: 6, spend: 300m, daysAgo: 5),
            Buyer(orders: 1, tickets: 1, spend: 55m, daysAgo: 10),
        };

        var result = _sut.Segment(buyers, AsOf, Window);

        result.Source.Should().Be(AnalyticsSource.Heuristic);
        result.Items.Single(s => s.Name == "Neaktivni kupci").Buyers.Should().Be(2);
    }

    /// <summary>Recency is measured against the range's last day, not today — a report for a past
    /// period must describe who those buyers were then.</summary>
    [Fact]
    public void Segment_MeasuresRecencyAgainstTheGivenDayRatherThanToday()
    {
        var buyers = Enumerable.Range(0, 4).Select(_ => Buyer(orders: 1, tickets: 1, spend: 60m, daysAgo: 10)).ToList();

        var result = _sut.Segment(buyers, AsOf, Window);

        result.Items.Should().OnlyContain(s => s.AverageRecencyDays == 10);
    }

    [Fact]
    public void Segment_NeverReportsANegativeRecency()
    {
        // A ticket bought in the last hours of the final day can convert to a local date sitting
        // just past the range's own midnight boundary.
        var buyers = Enumerable.Range(0, 4).Select(_ => Buyer(orders: 1, tickets: 1, spend: 60m, daysAgo: -1)).ToList();

        var result = _sut.Segment(buyers, AsOf, Window);

        result.Items.Should().OnlyContain(s => s.AverageRecencyDays >= 0);
    }
}
