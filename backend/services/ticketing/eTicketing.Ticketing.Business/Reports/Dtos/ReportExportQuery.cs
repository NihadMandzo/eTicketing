namespace eTicketing.Ticketing.Business.Reports;

/// <summary>The export request: a <see cref="ReportQuery"/> plus which tab to render. Separate
/// type (rather than a nullable Tab on ReportQuery) so the four data endpoints can't be called
/// with a stray tab parameter that would silently do nothing.</summary>
public sealed record ReportExportQuery : IReportRange
{
    public ReportTab Tab { get; init; }
    public DateOnly From { get; init; }
    public DateOnly To { get; init; }
}
