namespace eTicketing.Shared.TicketPdf;

/// <summary>
/// A table of pre-formatted strings. Deliberately not generic and not typed per report: the
/// document's job is layout, and every number reaching it has already been rounded and formatted
/// in Bosnian by the caller, so the PDF can never disagree with the screen about what "43,60 KM"
/// is.
/// </summary>
public sealed record ReportPdfTable(
    string Title,
    IReadOnlyList<ReportPdfColumn> Columns,
    IReadOnlyList<IReadOnlyList<string>> Rows,
    // Optional bold footer row (the design's "Ukupno" line). Must have the same arity as Columns.
    IReadOnlyList<string>? TotalsRow = null);
