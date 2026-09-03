using eTicketing.Ticketing.Business.Analytics.Forecasting;
using eTicketing.Ticketing.Business.Reports;
using Microsoft.Extensions.Logging;
using Microsoft.ML;
using static eTicketing.Ticketing.Business.Reports.ReportMath;

namespace eTicketing.Ticketing.Business.Analytics.Anomalies;

/// <summary>The SSA spike detector's output column: [Alert, Score, P-Value].</summary>
internal sealed class SpikePrediction
{
    [Microsoft.ML.Data.VectorType(3)]
    public double[] Prediction { get; set; } = [];
}

/// <summary>
/// ML.NET SSA spike detection over the daily revenue series.
///
/// <para><b>What "anomalous" means here.</b> Not "large" — a sold-out Saturday is not news. SSA
/// models the series' own trend and weekly rhythm and flags the days that departed from what that
/// model expected, so a big Saturday is ordinary and a big Tuesday is not. That distinction is the
/// entire value of using a time-series detector rather than a threshold.</para>
///
/// <para><b>Expected value is always the rolling median</b>, in both Model and Heuristic mode, even
/// though SSA also emits its own score. Two reasons: the card's sentence ("214% iznad očekivanog")
/// then means exactly the same thing on both rungs of the ladder, and a rolling median is a number
/// that can be checked by hand against the chart, which a latent SSA residual cannot.</para>
/// </summary>
public sealed class SsaAnomalyDetector : IAnomalyDetector
{
    /// <summary>Same 28-point threshold as the forecaster, for the same reason: below four
    /// observations per weekday, the seasonality being modelled is noise.</summary>
    internal const int MinPointsForModel = SsaSalesForecaster.MinPointsForModel;

    internal const int MinPointsForHeuristic = SsaSalesForecaster.MinPointsForHeuristic;

    private const int SeasonalityWindow = 7;

    /// <summary>Widest the "typical for this stretch" median window may be. Odd, so it is centred
    /// on the day being judged.</summary>
    private const int MedianWindow = 7;

    /// <summary>
    /// A year of a noisy series produces dozens of flagged days, which is a wall rather than an
    /// insight. Five is what fits the card and the PDF table without scrolling, and the ones that
    /// are cut are by construction the least surprising.
    /// </summary>
    internal const int MaxAnomalies = 5;

    /// <summary>Conventional robust-outlier cut-off: 3 scaled MADs from the median. Stricter than
    /// the usual 2.5 because a small organizer's series is spiky by nature and a detector that
    /// flags every third day is not one anybody reads twice.</summary>
    private const double RobustThreshold = 3.0;

    private readonly ILogger<SsaAnomalyDetector> _logger;

    public SsaAnomalyDetector(ILogger<SsaAnomalyDetector> logger) => _logger = logger;

    public AnomalyBlock Detect(IReadOnlyList<DailyPoint> history)
    {
        // Same two empty cases as the forecaster, and the second is the one that actually occurs:
        // ReportSeries.ToDailySeries zero-fills, so a quiet quarter is ninety zeroes rather than an
        // empty list. Nothing departed from anything there, and saying a model looked is a stronger
        // claim than the data supports.
        if (history.Count < MinPointsForHeuristic || history.All(p => p.Revenue == 0m))
            return new AnomalyBlock(AnalyticsSource.Insufficient, []);

        var revenue = history.Select(p => (double)p.Revenue).ToArray();
        var expected = RollingMedian(revenue);

        // The two detectors are unioned, not chosen between.
        //
        // SSA answers "did this day depart from the trend and weekly rhythm the series has been
        // following" — subtle departures a threshold cannot see. What it does not reliably do is
        // catch a gross outlier: its p-value for a tenfold spike against a steady baseline sits
        // right at the 95% boundary, so whether it fires comes down to numerical noise. That was
        // measured, not assumed — the same spike was found or missed run to run, deterministically
        // under an idle machine and not under a loaded one.
        //
        // The robust MAD check has the opposite profile: blind to seasonality, unmissable on gross
        // outliers, and fully deterministic. Running both and taking the union means an obviously
        // unusual day is never silently absent, which is the failure an organizer would actually
        // notice, while SSA still contributes everything only it can see.
        var robust = DetectRobust(revenue);
        var ssa = history.Count >= MinPointsForModel ? TryDetectSsa(revenue) : null;

        // Source names the strongest rung that ran, so it still says whether a model looked at all.
        var source = ssa is null ? AnalyticsSource.Heuristic : AnalyticsSource.Model;

        var items = robust
            .Concat(ssa ?? [])
            // Both may flag the same day; the more confident reading wins.
            .GroupBy(f => f.Index)
            .Select(g => (Index: g.Key, Confidence: g.Max(f => f.Confidence)))
            .Select(f => Build(history[f.Index], expected[f.Index], f.Confidence))
            // A run of days on which nothing at all happened is not an anomaly, however it scores:
            // both the observed and the expected figure are zero and there is nothing to report.
            .Where(a => a is not null)
            .Select(a => a!)
            .OrderByDescending(a => a.Confidence)
            .ThenByDescending(a => Math.Abs(a.Revenue - a.ExpectedRevenue))
            .Take(MaxAnomalies)
            // Ranked to decide which five survive, then re-ordered chronologically: the reader is
            // scanning a timeline, and five rows out of date order read as a bug.
            .OrderBy(a => a.Date)
            .ToList();

        return new AnomalyBlock(source, items);
    }

    private SalesAnomaly? Build(DailyPoint point, double expected, double confidence)
    {
        if (point.Revenue == 0m && expected == 0d)
            return null;

        var expectedDecimal = Round2((decimal)expected);
        var deviation = expected <= 0d
            // Nothing was expected and something happened: a percentage against a zero baseline is
            // undefined, so it is reported as a clean 100% rather than as an infinity the clients
            // would each have to special-case.
            ? 100m
            : Round2(((decimal)((double)point.Revenue - expected) / (decimal)expected) * 100m);

        return new SalesAnomaly(
            Date: point.Date,
            Label: ReportSeries.DayLabel(point.Date),
            Direction: (decimal)expected <= point.Revenue ? AnomalyDirection.Spike : AnomalyDirection.Drop,
            Revenue: Round2(point.Revenue),
            ExpectedRevenue: expectedDecimal,
            DeviationPercent: deviation,
            Confidence: Math.Round(Math.Clamp(confidence, 0d, 1d), 4));
    }

    /// <summary>
    /// Fits the SSA spike detector, returning null if it cannot.
    ///
    /// Same defensive stance as the forecaster: a near-constant series makes SSA's covariance
    /// matrix singular, and a report the user is entitled to see must not 500 because their month
    /// was quiet. Null drops the caller onto the robust rung.
    /// </summary>
    private List<(int Index, double Confidence)>? TryDetectSsa(double[] revenue)
    {
        try
        {
            var mlContext = new MLContext(seed: 0);
            var data = mlContext.Data.LoadFromEnumerable(revenue.Select(v => new SeriesRecord { Value = (float)v }));

            // pvalueHistoryLength is the window the p-value is estimated over — a quarter of the
            // series, floored at 4, so a 28-day range still has a usable history and a year-long one
            // does not judge August against January.
            var estimator = mlContext.Transforms.DetectSpikeBySsa(
                outputColumnName: nameof(SpikePrediction.Prediction),
                inputColumnName: nameof(SeriesRecord.Value),
                confidence: 95d,
                pvalueHistoryLength: Math.Max(4, revenue.Length / 4),
                trainingWindowSize: revenue.Length,
                seasonalityWindowSize: SeasonalityWindow);

            // Fitted on an empty view and then applied to the real one — the documented shape for
            // ML.NET's streaming detectors, which learn as they walk the data rather than in a
            // separate training pass.
            // The enumeration is inside the gate too, not just the fit: ML.NET's data views are
            // lazy, so the transform does not actually run until the rows are pulled.
            var predictions = MlGate.Run(() =>
            {
                var empty = mlContext.Data.LoadFromEnumerable(new List<SeriesRecord>());
                var transformed = estimator.Fit(empty).Transform(data);

                return mlContext.Data
                    .CreateEnumerable<SpikePrediction>(transformed, reuseRowObject: false)
                    .ToList();
            });

            var flagged = new List<(int, double)>();
            for (var i = 0; i < predictions.Count && i < revenue.Length; i++)
            {
                var prediction = predictions[i].Prediction;
                if (prediction.Length < 3 || prediction[0] == 0d)
                    continue;

                // prediction[2] is the p-value: how likely a series like this one produces a day
                // like this one. Inverted so a *less* likely day ranks higher.
                flagged.Add((i, 1d - prediction[2]));
            }

            return flagged;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "SSA detekcija anomalija nije uspjela na seriji od {Points} tačaka — koristi se robusna metoda.", revenue.Length);
            return null;
        }
    }

    /// <summary>
    /// Median absolute deviation: |x − median| / (1,4826 × MAD) &gt; 3.
    ///
    /// Chosen over a plain standard-deviation z-score because the thing being detected is exactly
    /// the thing that inflates a standard deviation — one huge day drags the mean and the spread up
    /// far enough to hide itself. The median and the MAD do not move, which is what makes this
    /// usable on the short, spiky series a small organizer actually has.
    ///
    /// The 1,4826 factor rescales the MAD so its threshold is comparable to a normal-distribution
    /// z-score, which is what lets the same "3" mean the same strictness on both rungs.
    /// </summary>
    private static List<(int Index, double Confidence)> DetectRobust(double[] revenue)
    {
        var median = Median(revenue);
        var mad = Median([.. revenue.Select(v => Math.Abs(v - median))]);
        var scale = 1.4826d * mad;

        // A series whose MAD is zero is more than half identical days. Any departure from that is
        // real, so the fallback becomes "anything that is not the median", which is the correct
        // reading and not a division by zero.
        if (scale <= 0d)
        {
            return
            [
                .. revenue
                    .Select((value, index) => (index, value))
                    .Where(x => Math.Abs(x.value - median) > 0d)
                    .Select(x => (x.index, 1d))
            ];
        }

        var flagged = new List<(int, double)>();
        for (var i = 0; i < revenue.Length; i++)
        {
            var z = Math.Abs(revenue[i] - median) / scale;
            if (z <= RobustThreshold)
                continue;

            // A ranking score, not a probability — normalized against twice the threshold so the
            // most extreme days saturate at 1 and the ordering stays meaningful.
            flagged.Add((i, Math.Clamp(z / (RobustThreshold * 2d), 0d, 1d)));
        }

        return flagged;
    }

    /// <summary>What each day "should" have been: the median of the window centred on it. Centred
    /// rather than trailing so a day is judged against the stretch it sits in rather than against
    /// only its past, which would report every rise as an anomaly on its first day.</summary>
    private static double[] RollingMedian(double[] values)
    {
        var half = MedianWindow / 2;
        var result = new double[values.Length];

        for (var i = 0; i < values.Length; i++)
        {
            var from = Math.Max(0, i - half);
            var to = Math.Min(values.Length - 1, i + half);
            result[i] = Median([.. values[from..(to + 1)]]);
        }

        return result;
    }

    private static double Median(double[] values)
    {
        if (values.Length == 0)
            return 0d;

        var sorted = (double[])values.Clone();
        Array.Sort(sorted);
        var mid = sorted.Length / 2;

        return sorted.Length % 2 == 1 ? sorted[mid] : (sorted[mid - 1] + sorted[mid]) / 2d;
    }
}
