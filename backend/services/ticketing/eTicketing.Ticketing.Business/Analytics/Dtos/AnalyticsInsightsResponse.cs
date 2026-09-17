using eTicketing.Ticketing.Business.Reports;

namespace eTicketing.Ticketing.Business.Analytics;

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
