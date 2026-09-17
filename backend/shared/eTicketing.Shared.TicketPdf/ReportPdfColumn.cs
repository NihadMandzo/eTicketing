namespace eTicketing.Shared.TicketPdf;

/// <summary>One column of the table. <paramref name="Weight"/> is a relative width, matching the
/// fractional grid columns of docs/Design/Reports.dc.html (e.g. 2.2fr for the name column).</summary>
public sealed record ReportPdfColumn(string Header, float Weight = 1f, bool RightAligned = false);
