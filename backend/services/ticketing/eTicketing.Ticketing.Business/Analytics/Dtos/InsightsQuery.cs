using eTicketing.Ticketing.Business.Reports;

namespace eTicketing.Ticketing.Business.Analytics;

/// <summary>
/// The AI Uvidi request. Carries the same range as every other report (so it inherits the whole of
/// ReportRangeValidator) plus how far past it to project.
/// </summary>
public sealed record InsightsQuery : IReportRange
{
    public DateOnly From { get; init; }
    public DateOnly To { get; init; }

    /// <summary>Days to forecast past <see cref="To"/> — 30, 90, 180 or 365, the four the desktop
    /// selector offers. An arbitrary horizon would let a caller ask SSA to extrapolate far past its
    /// own training window, which produces a confident-looking straight line.</summary>
    public int Horizon { get; init; } = 30;
}
