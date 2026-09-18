namespace eTicketing.Ticketing.Business.Analytics;

/// <summary>
/// One drawn point of the forecast series. <paramref name="LowerBound"/>/<paramref name="UpperBound"/>
/// are the 95% confidence interval — always rendered, because a forecast drawn without its
/// uncertainty reads as a promise.
///
/// A point is one day on a one-month horizon and one week, fortnight or month on the longer ones:
/// a year projected as 365 daily bars is 16,000px of chart nobody scrolls through. The bucketing
/// happens here rather than in each client so the desktop chart and the PDF cannot disagree about
/// what a bar covers, and <paramref name="Date"/> is always the first day the point covers.
/// <see cref="ForecastBlock.ProjectedRevenue"/> and <c>ProjectedSold</c> are the true daily totals
/// regardless — they are summed before the points are folded.
/// </summary>
public sealed record ForecastPoint(
    DateOnly Date, string Label, decimal Revenue, decimal LowerBound, decimal UpperBound, int Sold);
