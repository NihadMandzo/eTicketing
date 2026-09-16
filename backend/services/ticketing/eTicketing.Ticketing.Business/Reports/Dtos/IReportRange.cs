namespace eTicketing.Ticketing.Business.Reports;

/// <summary>What both report requests have in common, so the range rules (From &lt;= To, not in the
/// future, at most a year) can be written once as ReportRangeValidator&lt;T&gt; and applied to
/// both, rather than copied and left to drift apart.</summary>
public interface IReportRange
{
    DateOnly From { get; }
    DateOnly To { get; }
}
