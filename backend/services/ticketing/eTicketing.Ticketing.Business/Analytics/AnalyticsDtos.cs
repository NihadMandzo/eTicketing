using eTicketing.Ticketing.Business.Reports;

namespace eTicketing.Ticketing.Business.Analytics;

/// <summary>
/// Which strategy actually produced a block of the AI Uvidi tab.
///
/// Every block carries its own copy rather than the response carrying one for all of them: on a
/// young organization the forecast can legitimately be <see cref="Heuristic"/> while segmentation
/// is <see cref="Insufficient"/> and nothing about that is an error. The clients title each block
/// from this, which is the same reason RecommendationSource travels on the wire — a fallback that
/// cannot be seen looks like a bug when a demo shows something unexpected.
///
/// Serialized as the integer ordinal, like every other enum crossing this boundary, and mirrored
/// by ordinal in the frontend enum tables. Append only.
/// </summary>
public enum AnalyticsSource
{
    /// <summary>A fitted ML.NET model produced this block.</summary>
    Model,

    /// <summary>Not enough history for the model, but enough to say something honest — a moving
    /// average, a robust z-score, fixed RFM tiers.</summary>
    Heuristic,

    /// <summary>Too little data to claim anything at all. The block is empty and the UI says so
    /// rather than drawing an empty chart.</summary>
    Insufficient
}

/// <summary>Which way an anomalous day departed from what the series predicted.</summary>
public enum AnomalyDirection
{
    Spike,
    Drop
}

/// <summary>How an insight should read. Drives the card's colour and the ranking — a Critical
/// card is always above a Positive one, because the organizer opening this tab needs the problem
/// before the compliment.</summary>
public enum InsightSeverity
{
    Positive,
    Neutral,
    Warning,
    Critical
}

/// <summary>Which part of the business an insight is about. Purely for the card's icon and for
/// grouping in the PDF — no behavior hangs off it.</summary>
public enum InsightCategory
{
    Forecast,
    Anomaly,
    Sales,
    Audience,
    Redemption,
    Catalog
}

/// <summary>
/// The AI Uvidi request. Carries the same range as every other report (so it inherits the whole of
/// ReportRangeValidator) plus how far past it to project.
/// </summary>
public sealed record InsightsQuery : IReportRange
{
    public DateOnly From { get; init; }
    public DateOnly To { get; init; }

    /// <summary>Days to forecast past <see cref="To"/>. Restricted to the three the desktop
    /// selector offers — an arbitrary horizon would let a caller ask SSA to extrapolate a year
    /// past its own training window, which produces a confident-looking straight line.</summary>
    public int Horizon { get; init; } = 14;
}

/// <summary>One projected day. <paramref name="LowerBound"/>/<paramref name="UpperBound"/> are the
/// 95% confidence interval — always rendered, because a forecast drawn without its uncertainty
/// reads as a promise.</summary>
public sealed record ForecastPoint(
    DateOnly Date, string Label, decimal Revenue, decimal LowerBound, decimal UpperBound, int Sold);

/// <summary>
/// The forecast block. <paramref name="ChangePercent"/> compares the projected total against the
/// equally-long stretch of actuals immediately before it, so "+18,2%" always means "than the last
/// <c>Horizon</c> days" — null when that stretch sold nothing, same rule as the Prodaja tab.
/// </summary>
public sealed record ForecastBlock(
    AnalyticsSource Source,
    int Horizon,
    decimal ProjectedRevenue,
    int ProjectedSold,
    decimal? ChangePercent,
    // The tail of actuals the projection continues, so the client can draw one unbroken series
    // without re-deriving which days the Prodaja tab happened to bucket together.
    IReadOnlyList<ForecastPoint> Actual,
    IReadOnlyList<ForecastPoint> Points);

/// <summary>One unusual day. <paramref name="ExpectedRevenue"/> is what the series predicted, so
/// the card can say what was surprising rather than only that something was.</summary>
public sealed record SalesAnomaly(
    DateOnly Date,
    string Label,
    AnomalyDirection Direction,
    decimal Revenue,
    decimal ExpectedRevenue,
    decimal DeviationPercent,
    // 0-1. The SSA path reports 1 - p-value; the robust fallback reports a normalized z-score, so
    // the two are comparable enough to rank on and never presented as a probability.
    double Confidence);

public sealed record AnomalyBlock(AnalyticsSource Source, IReadOnlyList<SalesAnomaly> Items);

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

/// <summary>
/// The segmentation block. <paramref name="WindowLabel"/> exists because this block deliberately
/// ignores the selected range: recency and frequency over a 7-day window are noise, so segments
/// are always computed over the trailing 12 months ending at the range's last day. The UI prints
/// this label so the mismatch reads as a decision rather than a bug.
/// </summary>
public sealed record SegmentBlock(
    AnalyticsSource Source,
    string WindowLabel,
    int TotalBuyers,
    IReadOnlyList<AudienceSegment> Items);

/// <summary>One generated business insight — the thing the whole tab exists to produce.
/// <paramref name="Metric"/> is the pre-formatted figure the card shows as a chip ("12,4%",
/// "1.284,00 KM"), already Bosnian-formatted so no client re-derives it.</summary>
public sealed record BusinessInsight(
    InsightSeverity Severity,
    InsightCategory Category,
    string Title,
    string Body,
    string? Metric);

/// <summary>
/// Everything the AI Uvidi tab renders, from one request.
///
/// <paramref name="Narrative"/> is null whenever no LLM is configured, the call failed, or it
/// timed out — the tab is fully usable without it, and that is deliberate: the deterministic
/// insights are the feature, the narrative is a nicety on top.
/// </summary>
public sealed record AnalyticsInsightsResponse(
    ReportPeriod Period,
    string Scope,
    ForecastBlock Forecast,
    AnomalyBlock Anomalies,
    SegmentBlock Segments,
    IReadOnlyList<BusinessInsight> Insights,
    string? Narrative);
