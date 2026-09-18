namespace eTicketing.Ticketing.Business.Analytics;

/// <summary>One generated business insight — the thing the whole tab exists to produce.
/// <paramref name="Metric"/> is the pre-formatted figure the card shows as a chip ("12,4%",
/// "1.284,00 KM"), already Bosnian-formatted so no client re-derives it.</summary>
public sealed record BusinessInsight(
    InsightSeverity Severity,
    InsightCategory Category,
    string Title,
    string Body,
    string? Metric);
