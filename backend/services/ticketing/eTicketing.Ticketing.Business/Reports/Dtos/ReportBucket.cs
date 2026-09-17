namespace eTicketing.Ticketing.Business.Reports;

/// <summary>One bar of the sales chart. <paramref name="Label"/> is pre-formatted in Bosnian by
/// the server ("14. avg", "Avg") — the three clients would otherwise each need their own copy of
/// the month-name table and the bucket-labelling rules.</summary>
public sealed record ReportBucket(string Label, decimal Revenue, int Sold);
