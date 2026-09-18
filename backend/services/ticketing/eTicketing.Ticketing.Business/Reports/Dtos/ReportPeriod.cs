namespace eTicketing.Ticketing.Business.Reports;

/// <summary>Echoed back on every report so the client renders the range it actually got rather
/// than the one it thinks it asked for, and knows how to title the chart.</summary>
public sealed record ReportPeriod(DateOnly From, DateOnly To, int Days, ReportBucketUnit BucketUnit);
