namespace eTicketing.Ticketing.Business.Reports;

public sealed record OrganizationReportResponse(
    ReportPeriod Period,
    OrganizationReportView View,
    IReadOnlyList<OrganizationReportRow> Rows);
