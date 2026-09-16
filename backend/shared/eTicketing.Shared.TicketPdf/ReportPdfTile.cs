namespace eTicketing.Shared.TicketPdf;

/// <summary>One headline figure in the tile strip under the header.</summary>
public sealed record ReportPdfTile(
    string Label,
    string Value,
    string? Hint = null,
    ReportPdfEmphasis Emphasis = ReportPdfEmphasis.Neutral);
