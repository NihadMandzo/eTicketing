namespace eTicketing.Ticketing.Business.Reports;

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
