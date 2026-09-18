namespace eTicketing.Ticketing.Business.Reports;

/// <summary>Inclusive local calendar range. Bound from the query string by minimal APIs'
/// <c>[AsParameters]</c>; validated by ReportQueryValidator.</summary>
public sealed record ReportQuery : IReportRange
{
    public DateOnly From { get; init; }
    public DateOnly To { get; init; }
}
