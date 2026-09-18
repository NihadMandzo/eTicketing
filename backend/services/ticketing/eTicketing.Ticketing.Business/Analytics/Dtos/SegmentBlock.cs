namespace eTicketing.Ticketing.Business.Analytics;

/// <summary>
/// The segmentation block. <paramref name="WindowLabel"/> exists because this block deliberately
/// ignores the selected range: recency and frequency over a 7-day window are noise, so segments
/// are always computed over the trailing 12 months ending at the range's last day. The UI prints
/// this label so the mismatch reads as a decision rather than a bug.
/// </summary>
public sealed record SegmentBlock(
    AnalyticsSource Source,
    string WindowLabel,
    int TotalBuyers,
    IReadOnlyList<AudienceSegment> Items);
