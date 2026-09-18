namespace eTicketing.Ticketing.Business.Reports;

/// <summary>A rendered report PDF on its way out of GET /reports/export — bytes plus the filename
/// the download dialog should suggest. Same shape as TicketPdfService's result, for the same
/// reason: the endpoint sets the Content-Disposition header from it.</summary>
public sealed record ReportPdfResult(byte[] Content, string FileName);
