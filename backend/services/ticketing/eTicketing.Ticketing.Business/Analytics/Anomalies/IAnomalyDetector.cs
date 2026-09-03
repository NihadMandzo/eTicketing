using eTicketing.Ticketing.Business.Reports;

namespace eTicketing.Ticketing.Business.Analytics.Anomalies;

/// <summary>
/// Finds the days in a range whose revenue departed from what the rest of the series predicted.
///
/// Stateless and fitted per request, for the same reason as ISalesForecaster: the series being
/// judged is the one the user selected, so a stored model would be judging it against somebody
/// else's window.
/// </summary>
public interface IAnomalyDetector
{
    /// <summary>Never throws on thin or degenerate data — it degrades through the source ladder
    /// and reports which rung produced the result.</summary>
    AnomalyBlock Detect(IReadOnlyList<DailyPoint> history);
}
