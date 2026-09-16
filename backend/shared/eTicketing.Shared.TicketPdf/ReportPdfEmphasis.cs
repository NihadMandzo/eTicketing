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
