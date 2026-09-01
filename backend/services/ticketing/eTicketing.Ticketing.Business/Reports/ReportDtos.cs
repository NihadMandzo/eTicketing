namespace eTicketing.Ticketing.Business.Reports;

/// <summary>Which report is being asked for. Doubles as the <c>?tab=</c> value of
/// GET /reports/export, so the PDF and the screen name the same four things.</summary>
public enum ReportTab
{
    Sales,
    Products,
    Redemption,
    Organizations
}

/// <summary>How the sales chart is bucketed. Chosen from the range length rather than requested by
/// the client: 90 daily bars are unreadable and 3 monthly ones are uninformative, so the server
/// picks the unit that fits and tells the client which one it picked (the chart title says
/// "po Danu"/"po Sedmici"/"po Mjesecu").</summary>
public enum ReportBucketUnit
{
    Day,
    Week,
    Month
}

/// <summary>Which column set the Organizations tab should render. SuperAdmin sees the financial
/// view (tickets, revenue, growth), Admin the operational one (published/pending/no-image),
/// mirroring the two-column-set split in docs/Design/Reports.dc.html.</summary>
public enum OrganizationReportView
{
    Financial,
    Operational
}

/// <summary>What both report requests have in common, so the range rules (From &lt;= To, not in the
/// future, at most a year) can be written once as ReportRangeValidator&lt;T&gt; and applied to
/// both, rather than copied and left to drift apart.</summary>
public interface IReportRange
{
    DateOnly From { get; }
    DateOnly To { get; }
}

/// <summary>Inclusive local calendar range. Bound from the query string by minimal APIs'
/// <c>[AsParameters]</c>; validated by ReportQueryValidator.</summary>
public sealed record ReportQuery : IReportRange
{
    public DateOnly From { get; init; }
    public DateOnly To { get; init; }
}

/// <summary>The export request: a <see cref="ReportQuery"/> plus which tab to render. Separate
/// type (rather than a nullable Tab on ReportQuery) so the four data endpoints can't be called
/// with a stray tab parameter that would silently do nothing.</summary>
public sealed record ReportExportQuery : IReportRange
{
    public ReportTab Tab { get; init; }
    public DateOnly From { get; init; }
    public DateOnly To { get; init; }
}

/// <summary>Echoed back on every report so the client renders the range it actually got rather
/// than the one it thinks it asked for, and knows how to title the chart.</summary>
public sealed record ReportPeriod(DateOnly From, DateOnly To, int Days, ReportBucketUnit BucketUnit);

/// <summary>One bar of the sales chart. <paramref name="Label"/> is pre-formatted in Bosnian by
/// the server ("14. avg", "Avg") — the three clients would otherwise each need their own copy of
/// the month-name table and the bucket-labelling rules.</summary>
public sealed record ReportBucket(string Label, decimal Revenue, int Sold);

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
    IReadOnlyList<ReportBucket> Buckets);

/// <summary>One row of the Dashboard's "Nadolazeći događaji" card — a published SingleOccurrence
/// product with a future date. <paramref name="Meta"/> is the small second line: "{organization} ·
/// {city}" for platform staff (who see every organization), "{N sektora} · {city}" for an organizer
/// (who already knows it's their own). <paramref name="Sold"/>/<paramref name="Capacity"/> reflect
/// the event's current state, not a reporting-period figure — see
/// ReportService.GetUpcomingEventsAsync.</summary>
public sealed record UpcomingEventResponse(
    Guid ProductId, string Name, string Meta, DateTime Date, int Sold, int Capacity);

/// <summary>
/// One row of the Učinak Proizvoda table. <paramref name="Meta"/> is the small grey second line:
/// the owning organization for platform staff, "N sektora · M karata" for an organizer looking at
/// their own products.
/// </summary>
public sealed record ProductReportRow(
    Guid ProductId,
    string Name,
    string Meta,
    int Sold,
    // Null for DailyEntry products, whose capacity is a per-day allowance rather than a total —
    // see ISectorRepository.GetPublishedCapacityByProductAsync. The UI renders "—".
    decimal? OccupancyPercent,
    decimal AveragePrice,
    int Cancelled,
    decimal Revenue);

public sealed record ProductReportResponse(
    ReportPeriod Period,
    string Scope,
    IReadOnlyList<ProductReportRow> Rows,
    int TotalSold,
    // Mean of the rows that have an occupancy at all; null when none do.
    decimal? AverageOccupancyPercent,
    decimal AveragePrice,
    int TotalCancelled,
    decimal TotalRevenue);

/// <summary>One bar of the Dolazak po Satu chart. <paramref name="Hour"/> is 0-23 local.</summary>
public sealed record CheckinHourPoint(int Hour, int Count);

public sealed record RedemptionReportRow(
    Guid ProductId,
    string Name,
    int Sold,
    int CheckedIn,
    int NoShow,
    int Printed,
    decimal RatePercent);

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

/// <summary>
/// One row of the Organizacije table. The two column sets of the design are folded into one row
/// type with nullable halves rather than two separate response shapes: which half is populated is
/// stated once by <see cref="OrganizationReportResponse.View"/>, and both clients and the PDF read
/// the same record. Nullable (rather than zero-filled) so an unpopulated column is visibly absent
/// instead of claiming a real zero.
/// </summary>
public sealed record OrganizationReportRow(
    Guid OrganizationId,
    string Name,
    string Address,
    int Products,
    // Financial view (SuperAdmin)
    int? Tickets,
    decimal? AveragePrice,
    decimal? GrowthPercent,
    decimal? Revenue,
    // Operational view (Admin)
    int? Published,
    int? Pending,
    int? WithoutImage,
    bool? IsPending);

public sealed record OrganizationReportResponse(
    ReportPeriod Period,
    OrganizationReportView View,
    IReadOnlyList<OrganizationReportRow> Rows);

/// <summary>A rendered report PDF on its way out of GET /reports/export — bytes plus the filename
/// the download dialog should suggest. Same shape as TicketPdfService's result, for the same
/// reason: the endpoint sets the Content-Disposition header from it.</summary>
public sealed record ReportPdfResult(byte[] Content, string FileName);
