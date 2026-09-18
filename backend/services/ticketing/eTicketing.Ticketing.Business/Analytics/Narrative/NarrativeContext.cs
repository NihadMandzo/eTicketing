using eTicketing.Ticketing.Business.Reports;
using static eTicketing.Ticketing.Business.Reports.ReportFormatting;

namespace eTicketing.Ticketing.Business.Analytics.Narrative;

/// <summary>The facts a narrative may be written from — and, by construction, the only ones.</summary>
public sealed record NarrativeContext(
    string Scope,
    ReportPeriod Period,
    SalesReportResponse Sales,
    ForecastBlock Forecast,
    AnomalyBlock Anomalies,
    SegmentBlock Segments,
    IReadOnlyList<BusinessInsight> Insights);
