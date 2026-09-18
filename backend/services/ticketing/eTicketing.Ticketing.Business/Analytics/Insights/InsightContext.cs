using eTicketing.Ticketing.Business.Reports;
using static eTicketing.Ticketing.Business.Reports.ReportFormatting;
using static eTicketing.Ticketing.Business.Reports.ReportMath;

namespace eTicketing.Ticketing.Business.Analytics.Insights;

/// <summary>
/// Everything the rules read. The three analytics blocks plus the three descriptive reports, passed
/// in rather than re-queried — the tab has already paid for those figures, and recomputing them
/// here is how the insight card and the tile above it end up disagreeing about the same number.
///
/// <paramref name="Redemption"/> is null for OrganizationAdmin, the one role that may see this tab
/// but not gate statistics. The no-show rule simply does not fire rather than the tab refusing.
/// </summary>
public sealed record InsightContext(
    ForecastBlock Forecast,
    AnomalyBlock Anomalies,
    SegmentBlock Segments,
    SalesReportResponse Sales,
    ProductReportResponse Products,
    RedemptionReportResponse? Redemption);
