namespace eTicketing.Ticketing.Business.Reports;

/// <summary>
/// The Iskorištenost Karata tab. <paramref name="PeakHour"/> is null when nothing was scanned in
/// the range at all — there is no busiest hour of an empty histogram, and the tile shows "—"
/// rather than a misleading "00:00".
/// </summary>
public sealed record RedemptionReportResponse(
    ReportPeriod Period,
    string Scope,
    int TotalCheckedIn,
    decimal NoShowRatePercent,
    int? PeakHour,
    decimal? PeakHourSharePercent,
    int PrintedTickets,
    IReadOnlyList<CheckinHourPoint> CheckinsByHour,
    IReadOnlyList<RedemptionReportRow> Rows);
