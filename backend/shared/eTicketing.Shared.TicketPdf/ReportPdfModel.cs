namespace eTicketing.Shared.TicketPdf;

/// <summary>
/// Everything <see cref="ReportDocument"/> needs to draw one exported report, in a shape that
/// carries no domain types at all.
///
/// That is the point: this project sits below eTicketing.Ticketing.Business and cannot reference
/// it, and the reports have different shapes. Rather than one document per report (or one document
/// that knows about tickets, organizations and check-ins), the caller flattens whichever report it
/// is into this common tile/chart/table vocabulary and the renderer stays a renderer.
///
/// <para><paramref name="Summary"/> and <paramref name="Notes"/> were added for the AI Uvidi tab,
/// which is a page of findings rather than a page of figures. They stay generic — a lead paragraph
/// and a list of titled blocks — precisely so the next report needing prose does not add a third
/// concept.</para>
/// </summary>
public sealed record ReportPdfModel(
    string Title,
    string Scope,
    string RangeLabel,
    string GeneratedAtLabel,
    IReadOnlyList<ReportPdfTile> Tiles,
    string? ChartTitle = null,
    IReadOnlyList<ReportPdfBar>? Chart = null,
    ReportPdfTable? Table = null,
    string? Summary = null,
    string? NotesTitle = null,
    IReadOnlyList<ReportPdfNote>? Notes = null);
