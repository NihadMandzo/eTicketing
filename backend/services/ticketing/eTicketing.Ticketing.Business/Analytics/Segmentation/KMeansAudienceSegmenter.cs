using eTicketing.Ticketing.Data.Repositories;
using Microsoft.Extensions.Logging;
using Microsoft.ML;
using Microsoft.ML.Data;
using Microsoft.ML.Trainers;
using static eTicketing.Ticketing.Business.Reports.ReportMath;

namespace eTicketing.Ticketing.Business.Analytics.Segmentation;

/// <summary>
/// ML.NET K-Means over per-buyer RFM features.
///
/// <para><b>Why clustering and not fixed tiers.</b> Fixed thresholds ("a big spender is anyone over
/// 200 KM") encode one operator's guess about one market. K-Means finds the groups this
/// organization's buyers actually fall into, so a parking operator selling 30 KM monthly spaces and
/// a festival selling 200 KM passes both get four meaningful segments without anybody tuning a
/// constant. The fixed tiers still exist — as the fallback for when there are too few buyers to
/// find structure in.</para>
///
/// <para><b>Segments are named from centroid rank, never from cluster index.</b> K-Means numbers
/// its clusters by whatever order the initialization happened to visit them; the same data can hand
/// the same group a different index on a rerun. Naming by index would silently relabel every
/// segment between two page loads. Ranking the centroids by value first makes the names stable, and
/// there is a test that asserts exactly that.</para>
/// </summary>
public sealed class KMeansAudienceSegmenter : IAudienceSegmenter
{
    /// <summary>
    /// Fewest buyers K-Means is allowed to run on.
    ///
    /// Not a library limit — K-Means will happily partition eight points into four clusters. It is a
    /// meaningfulness limit: below roughly five points per cluster the "segments" are individuals
    /// with names attached, and telling an organizer that "Kupci visoke vrijednosti" is 20% of their
    /// audience when it is one person is worse than telling them nothing.
    /// </summary>
    internal const int MinBuyersForModel = 20;

    private const int ClusterCount = 4;

    /// <summary>A buyer who has not returned in half a year. Long enough that a seasonal
    /// festival-goer who comes every summer is not written off after one quiet spring.</summary>
    private const int DormantAfterDays = 180;

    /// <summary>
    /// Segment names in descending order of value, assigned to the centroids after they are ranked.
    /// The vocabulary is fixed rather than generated so the same four names appear on the screen,
    /// in the PDF and in the LLM prompt, and an organizer learns what they mean once.
    /// </summary>
    private static readonly string[] SegmentNames =
        ["Kupci visoke vrijednosti", "Redovni kupci", "Povremeni kupci", "Neaktivni kupci"];

    private readonly ILogger<KMeansAudienceSegmenter> _logger;

    public KMeansAudienceSegmenter(ILogger<KMeansAudienceSegmenter> logger) => _logger = logger;

    public SegmentBlock Segment(IReadOnlyList<BuyerFacts> buyers, DateOnly asOf, string windowLabel)
    {
        if (buyers.Count == 0)
            return new SegmentBlock(AnalyticsSource.Insufficient, windowLabel, 0, []);

        var profiles = buyers.Select(b => Profile(b, asOf)).ToList();

        if (profiles.Count >= MinBuyersForModel)
        {
            var clustered = TryCluster(profiles);
            if (clustered is not null)
                return new SegmentBlock(AnalyticsSource.Model, windowLabel, profiles.Count, clustered);
        }

        return new SegmentBlock(AnalyticsSource.Heuristic, windowLabel, profiles.Count, Tiers(profiles));
    }

    /// <summary>
    /// Fits K-Means and returns null rather than throwing if it cannot.
    ///
    /// The realistic failure is a feature column with zero variance — every buyer bought once, on
    /// the same day, for the same price, which is exactly what a freshly seeded demo database looks
    /// like. NormalizeMinMax divides by the range and K-Means then has nothing to separate on.
    /// Dropping to the fixed tiers is the honest answer there, not a 500.
    /// </summary>
    private List<AudienceSegment>? TryCluster(List<BuyerProfile> profiles)
    {
        try
        {
            var mlContext = new MLContext(seed: 0);
            var data = mlContext.Data.LoadFromEnumerable(profiles.Select(p => p.Features));

            var pipeline = mlContext.Transforms
                .Concatenate(
                    "Features",
                    nameof(BuyerFeatures.RecencyDays),
                    nameof(BuyerFeatures.Orders),
                    nameof(BuyerFeatures.Spend),
                    nameof(BuyerFeatures.AverageTicketPrice),
                    nameof(BuyerFeatures.TicketsPerOrder))
                // Without this, Spend (hundreds of KM) would dominate the Euclidean distance and
                // Orders (single digits) would contribute nothing — the clusterer would be
                // partitioning on money alone and the other four features would be decoration.
                .Append(mlContext.Transforms.NormalizeMinMax("Features"))
                .Append(mlContext.Clustering.Trainers.KMeans(new KMeansTrainer.Options
                {
                    FeatureColumnName = "Features",
                    NumberOfClusters = ClusterCount,
                    MaximumNumberOfIterations = 100,
                    // Single-threaded so two runs over identical data produce identical clusters.
                    // Multi-threaded K-Means accumulates centroid updates in nondeterministic order,
                    // and the segment shares on screen would drift by a percent between refreshes
                    // with nothing having changed.
                    NumberOfThreads = 1,
                }));

            // Under the same gate as the two SSA paths. The concurrency defect was demonstrated on
            // time series rather than here, but it is the same library and the same shape of use,
            // and a segmentation that shifts depending on who else is looking would be the harder
            // of the two to notice.
            var assignments = MlGate.Run(() =>
            {
                var transformed = pipeline.Fit(data).Transform(data);
                return mlContext.Data
                    .CreateEnumerable<BuyerCluster>(transformed, reuseRowObject: false)
                    .Select(c => c.ClusterId)
                    .ToList();
            });

            if (assignments.Count != profiles.Count)
                return null;

            var groups = profiles
                .Zip(assignments, (profile, cluster) => (profile, cluster))
                .GroupBy(x => x.cluster)
                .Select(g => g.Select(x => x.profile).ToList())
                .ToList();

            return Name(groups, profiles);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "K-Means segmentacija nije uspjela za {Buyers} kupaca — koriste se fiksni RFM nivoi.", profiles.Count);
            return null;
        }
    }

    /// <summary>
    /// Ranks the clusters and hands out the fixed names.
    ///
    /// The ordering key is average spend first, then purchase frequency, then how recently the
    /// group last bought — the same order a person reading the table would rank them in, and the
    /// only part of the naming that is a judgement call. Everything downstream (the description,
    /// the insight rules, the LLM prompt) reads the name, so this is the one place that decides
    /// what "high value" means.
    /// </summary>
    private static List<AudienceSegment> Name(List<List<BuyerProfile>> groups, List<BuyerProfile> all)
    {
        var totalSpend = all.Sum(p => p.Spend);

        return
        [
            .. groups
                .Where(g => g.Count > 0)
                .OrderByDescending(g => g.Average(p => p.Spend))
                .ThenByDescending(g => g.Average(p => p.Orders))
                .ThenBy(g => g.Average(p => p.RecencyDays))
                .Select((group, rank) => Describe(
                    // More clusters than names cannot happen (ClusterCount == SegmentNames.Length),
                    // but an empty cluster means fewer — so the names are taken from the front and
                    // the tail is simply unused.
                    SegmentNames[Math.Min(rank, SegmentNames.Length - 1)],
                    group,
                    all.Count,
                    totalSpend))
        ];
    }

    /// <summary>
    /// Fixed RFM tiers — the fallback when there are too few buyers for clustering to mean anything.
    ///
    /// Evaluated in order, first match wins: dormancy overrides everything (a buyer who spent a
    /// fortune and vanished is a churn problem, not a VIP), then value against this
    /// organization's own median, then repeat custom, then everyone else. Empty tiers are dropped
    /// rather than shown as a 0% row.
    /// </summary>
    private static List<AudienceSegment> Tiers(List<BuyerProfile> profiles)
    {
        var medianSpend = MedianSpend(profiles);
        var totalSpend = profiles.Sum(p => p.Spend);

        var buckets = new Dictionary<string, List<BuyerProfile>>();
        foreach (var profile in profiles)
        {
            var name = profile switch
            {
                { RecencyDays: > DormantAfterDays } => "Neaktivni kupci",
                _ when medianSpend > 0m && profile.Spend >= medianSpend * 2m => "Kupci visoke vrijednosti",
                { Orders: >= 2 } => "Redovni kupci",
                _ => "Povremeni kupci"
            };

            if (!buckets.TryGetValue(name, out var bucket))
                buckets[name] = bucket = [];
            bucket.Add(profile);
        }

        return
        [
            .. SegmentNames
                .Where(buckets.ContainsKey)
                .Select(name => Describe(name, buckets[name], profiles.Count, totalSpend))
        ];
    }

    private static AudienceSegment Describe(
        string name, List<BuyerProfile> group, int totalBuyers, decimal totalSpend)
    {
        var averageSpend = Round2(group.Average(p => p.Spend));
        var averageOrders = group.Average(p => (decimal)p.Orders);
        var averageTickets = Round2(group.Average(p => (decimal)p.Tickets));
        var averageRecency = (int)Math.Round(group.Average(p => (double)p.RecencyDays), MidpointRounding.AwayFromZero);
        var groupSpend = group.Sum(p => p.Spend);

        return new AudienceSegment(
            Name: name,
            Description: Sentence(averageOrders, averageRecency, averageSpend),
            Buyers: group.Count,
            SharePercent: Round2(Divide(group.Count, totalBuyers) * 100m),
            RevenueSharePercent: Round2(Divide(groupSpend, totalSpend) * 100m),
            AverageSpend: averageSpend,
            AverageTickets: averageTickets,
            AverageRecencyDays: averageRecency);
    }

    /// <summary>The card's grey second line. Built from the group's own centroid rather than
    /// canned per name, so a segment always describes the buyers actually in it — the names are
    /// stable labels, but what "Redovni kupci" looks like differs between a parking operator and a
    /// festival.</summary>
    private static string Sentence(decimal averageOrders, int averageRecencyDays, decimal averageSpend)
    {
        var frequency = averageOrders switch
        {
            >= 4m => "kupuju često",
            >= 2m => "kupuju povremeno više puta",
            _ => "kupili su jednom"
        };

        var recency = averageRecencyDays switch
        {
            > DormantAfterDays => "nisu se vratili duže od šest mjeseci",
            > 90 => "posljednja kupovina prije više od tri mjeseca",
            > 30 => "posljednja kupovina u zadnjih nekoliko mjeseci",
            _ => "aktivni su u zadnjih mjesec dana"
        };

        return $"Prosječno {averageSpend:N2} KM po kupcu — {frequency}, {recency}.";
    }

    private static BuyerProfile Profile(BuyerFacts facts, DateOnly asOf)
    {
        // Recency against the range's own last day, never against today. Clamped at 0 because a
        // ticket bought late on the final day converts to a local date that can sit a few hours
        // past the range's midnight boundary.
        var recencyDays = Math.Max(0, asOf.DayNumber - DateOnly.FromDateTime(facts.LastPurchaseUtc).DayNumber);
        var averagePrice = facts.Tickets == 0 ? 0m : facts.Spend / facts.Tickets;
        var ticketsPerOrder = facts.Orders == 0 ? 0m : (decimal)facts.Tickets / facts.Orders;

        return new BuyerProfile(
            recencyDays,
            facts.Orders,
            facts.Tickets,
            facts.Spend,
            new BuyerFeatures
            {
                RecencyDays = recencyDays,
                Orders = facts.Orders,
                Spend = (float)facts.Spend,
                AverageTicketPrice = (float)averagePrice,
                TicketsPerOrder = (float)ticketsPerOrder,
            });
    }

    private static decimal MedianSpend(List<BuyerProfile> profiles)
    {
        var sorted = profiles.Select(p => p.Spend).OrderBy(v => v).ToList();
        if (sorted.Count == 0)
            return 0m;

        var mid = sorted.Count / 2;
        return sorted.Count % 2 == 1 ? sorted[mid] : (sorted[mid - 1] + sorted[mid]) / 2m;
    }

    /// <summary>The features the model reads, alongside the same facts in the units the card
    /// prints — so nothing downstream has to convert a normalized float back into money.</summary>
    private readonly record struct BuyerProfile(
        int RecencyDays, int Orders, int Tickets, decimal Spend, BuyerFeatures Features);
}
