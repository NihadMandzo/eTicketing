namespace eTicketing.Ticketing.Business.Reports;

/// <summary>Which report is being asked for. Doubles as the <c>?tab=</c> value of
/// GET /reports/export, so the PDF and the screen name the same things.
///
/// <para>Bound by name from the query string and mirrored by name in the desktop client's
/// <c>ReportTab.wireName</c> — append only, never reorder or rename.</para></summary>
public enum ReportTab
{
    Sales,
    Products,
    Redemption,
    Organizations,

    /// <summary>AI Uvidi — forecast, anomalies, audience segments and generated business
    /// insights. Not a descriptive aggregate like the four above, but it shares their range,
    /// their access matrix and their export path, so it belongs in the same enum.</summary>
    Insights
}
