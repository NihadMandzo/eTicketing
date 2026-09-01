namespace eTicketing.Shared.TicketPdf;

/// <summary>How a figure should read: neutral, good news, or bad news. Kept as an enum rather than
/// a colour string so the palette stays owned by <see cref="ReportDocument"/> and callers in
/// eTicketing.Ticketing never hardcode a hex value.</summary>
public enum ReportPdfEmphasis
{
    Neutral,
    Positive,
    Negative
}

/// <summary>One headline figure in the tile strip under the header.</summary>
public sealed record ReportPdfTile(
    string Label,
    string Value,
    string? Hint = null,
    ReportPdfEmphasis Emphasis = ReportPdfEmphasis.Neutral);

/// <summary>One bar of the chart. <paramref name="Ratio"/> is 0-1 relative to the tallest bar —
/// scaling is the caller's job, because only it knows whether the series is money or a count.</summary>
public sealed record ReportPdfBar(string Label, string Value, float Ratio);

/// <summary>One column of the table. <paramref name="Weight"/> is a relative width, matching the
/// fractional grid columns of docs/Design/Reports.dc.html (e.g. 2.2fr for the name column).</summary>
public sealed record ReportPdfColumn(string Header, float Weight = 1f, bool RightAligned = false);

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

/// <summary>
/// Everything <see cref="ReportDocument"/> needs to draw one exported report, in a shape that
/// carries no domain types at all.
///
/// That is the point: this project sits below eTicketing.Ticketing.Business and cannot reference
/// it, and the four reports have four different shapes. Rather than four documents (or one
/// document that knows about tickets, organizations and check-ins), the caller flattens whichever
/// report it is into this common tile/chart/table vocabulary and the renderer stays a renderer.
/// </summary>
public sealed record ReportPdfModel(
    string Title,
    string Scope,
    string RangeLabel,
    string GeneratedAtLabel,
    IReadOnlyList<ReportPdfTile> Tiles,
    string? ChartTitle = null,
    IReadOnlyList<ReportPdfBar>? Chart = null,
    ReportPdfTable? Table = null);
