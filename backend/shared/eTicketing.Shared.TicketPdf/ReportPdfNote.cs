namespace eTicketing.Shared.TicketPdf;

/// <summary>
/// One titled block of prose — an insight card in the AI Uvidi export. Tinted by
/// <paramref name="Emphasis"/> in the same three-colour vocabulary the tiles use, so the reader
/// can tell a warning from a compliment without reading it first.
/// </summary>
public sealed record ReportPdfNote(string Title, string Body, ReportPdfEmphasis Emphasis);
