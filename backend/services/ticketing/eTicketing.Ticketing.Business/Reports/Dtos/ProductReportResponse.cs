namespace eTicketing.Ticketing.Business.Reports;

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
