namespace eTicketing.Shared.TicketPdf;

/// <summary>One bar of the chart. <paramref name="Ratio"/> is 0-1 relative to the tallest bar —
/// scaling is the caller's job, because only it knows whether the series is money or a count.</summary>
public sealed record ReportPdfBar(string Label, string Value, float Ratio);
