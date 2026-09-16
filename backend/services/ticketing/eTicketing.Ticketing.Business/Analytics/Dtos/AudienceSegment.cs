namespace eTicketing.Ticketing.Business.Analytics;

/// <summary>
/// One audience segment. Named from its centroid's rank, never from its cluster index — K-Means
/// numbers its clusters arbitrarily and a rerun on the same data can hand the same group a
/// different index, which would silently rename every segment between two page loads.
/// </summary>
public sealed record AudienceSegment(
    string Name,
    string Description,
    int Buyers,
    decimal SharePercent,
    decimal RevenueSharePercent,
    decimal AverageSpend,
    decimal AverageTickets,
    int AverageRecencyDays);
