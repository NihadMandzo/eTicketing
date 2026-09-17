namespace eTicketing.Ticketing.Business.Reports;

/// <summary>
/// The Prodaja tab. Cancelled tickets are reported alongside the gross figure rather than
/// subtracted from it: <paramref name="GrossRevenue"/> is what was sold, and
/// <paramref name="NetRevenue"/> is what is left after cancellations, so both numbers are visible
/// instead of one netted one.
/// </summary>
public sealed record SalesReportResponse(
    ReportPeriod Period,
    string Scope,
    decimal GrossRevenue,
    int TicketsSold,
    decimal AverageTicketPrice,
    // Change against the immediately preceding equal-length period. Null when that period sold
    // nothing — "+∞%" is not a growth figure, and the UI drops the line instead.
    decimal? RevenueChangePercent,
    int CancelledCount,
    decimal CancelledAmount,
    decimal CancellationRatePercent,
    decimal NetRevenue,
    // Sales-channel split — the two real TicketOrigin values, both counted the same way
    // TicketsSold is (Confirmed/Ready/Used only). OnlineSold + PrintedSold == TicketsSold.
    int OnlineSold,
    int PrintedSold,
    IReadOnlyList<ReportBucket> Buckets,
    // Empty for an organizer (see SalesByOrganizationRow). The UI omits the section rather than
    // rendering an empty table.
    IReadOnlyList<SalesByOrganizationRow> ByOrganization);
