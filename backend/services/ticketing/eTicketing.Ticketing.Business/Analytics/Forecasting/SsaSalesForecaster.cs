using eTicketing.Ticketing.Business.Reports;
using Microsoft.Extensions.Logging;
using Microsoft.ML;
using Microsoft.ML.Transforms.TimeSeries;
using static eTicketing.Ticketing.Business.Reports.ReportMath;

namespace eTicketing.Ticketing.Business.Analytics.Forecasting;

/// <summary>One point of a series as ML.NET sees it — a single float column, which is all SSA
/// consumes. The dates live outside the model: SSA reads position as time, which is exactly why
/// ReportSeries.ToDailySeries zero-fills the gaps before anything reaches here.</summary>
internal sealed class SeriesRecord
{
    public float Value { get; set; }
}

internal sealed class SeriesForecast
{
    public float[] Forecast { get; set; } = [];
    public float[] LowerBound { get; set; } = [];
    public float[] UpperBound { get; set; } = [];
}

/// <summary>
/// ML.NET Singular Spectrum Analysis over the daily revenue and ticket series.
///
/// <para><b>Why SSA and not matrix factorization or a regression.</b> The question is "what does
/// this series do next", and the series has a strong weekly shape (weekend events, payday spikes)
/// on top of a trend. SSA decomposes exactly that — trend plus periodic components plus noise —
/// from a single univariate series with no feature engineering, and it hands back a confidence
/// interval, which a plain linear extrapolation cannot.</para>
///
/// <para><b>Why it is fitted per request and never persisted.</b> The recommender persists its
/// model because factorizing every user × product interaction is expensive and cannot be done per
/// request. SSA over at most 366 points is milliseconds, and — more importantly — the right
/// training window *is* the range the user selected. A stored model would be fitted on somebody
/// else's range. So there is no blob container, no snapshot table and no nightly job here, and
/// that is a deliberate difference from Catalog's recommender rather than an omission.</para>
///
/// <para><b>The source ladder.</b> Model → Heuristic → Insufficient, reported on the block, for the
/// same reason RecommendationSource travels on the wire: an organizer two weeks into using the
/// platform should be told their forecast is a moving average, not shown a straight line with the
/// same authority as a fitted one.</para>
/// </summary>
public sealed class SsaSalesForecaster : ISalesForecaster
{
    /// <summary>Weekly seasonality — the dominant period in ticket sales, and the only one a
    /// range as short as a month can support.</summary>
    private const int WindowSize = 7;

    /// <summary>
    /// Fewest daily points SSA is allowed to run on.
    ///
    /// ML.NET requires <c>trainSize &gt; 2 × windowSize</c>, so 15 would satisfy the library. 28 is
    /// the threshold used instead because it is the smallest window containing four observations of
    /// every weekday: below that, the weekly component SSA is being asked to extract is fitted on
    /// two or three samples per weekday and the "seasonality" it finds is noise.
    /// </summary>
    internal const int MinPointsForModel = 28;

    /// <summary>Below this there is nothing honest to say at all — a moving average over three
    /// days is not a forecast, it is the last three days restated.</summary>
    internal const int MinPointsForHeuristic = 7;

    private readonly ILogger<SsaSalesForecaster> _logger;

    public SsaSalesForecaster(ILogger<SsaSalesForecaster> logger) => _logger = logger;

    public ForecastBlock Forecast(IReadOnlyList<DailyPoint> history, int horizon)
    {
        // Two ways to have nothing to say: too few days, or plenty of days in which nothing ever
        // happened. The second matters as much as the first — ReportSeries.ToDailySeries zero-fills
        // the range, so a quiet quarter arrives here as ninety perfectly valid zeroes. SSA would fit
        // them and return a flat zero labelled Model, which reads as an authoritative statement
        // about a period containing no information at all.
        if (history.Count < MinPointsForHeuristic || history.All(p => p.Revenue == 0m && p.Sold == 0))
            return Empty(horizon, AnalyticsSource.Insufficient);

        var lastDate = history[^1].Date;
        var revenue = history.Select(p => (float)p.Revenue).ToArray();
        var sold = history.Select(p => (float)p.Sold).ToArray();

        var (revenueForecast, source) = Project(revenue, history, horizon);
        var soldForecast = source == AnalyticsSource.Model
            ? TryFitSsa(sold, horizon) ?? Heuristic(sold, history, horizon)
            : Heuristic(sold, history, horizon);

        var daily = new List<ForecastPoint>(horizon);
        for (var i = 0; i < horizon; i++)
        {
            var date = lastDate.AddDays(i + 1);
            daily.Add(new ForecastPoint(
                Date: date,
                Label: ReportSeries.DayLabel(date),
                Revenue: Round2(NonNegative(revenueForecast.Values[i])),
                LowerBound: Round2(NonNegative(revenueForecast.Lower[i])),
                UpperBound: Round2(NonNegative(revenueForecast.Upper[i])),
                // Tickets are whole things; a projection of 4,7 karata is rounded rather than
                // truncated so a horizon's points still sum to roughly its total.
                Sold: (int)Math.Round(Math.Max(0f, soldForecast.Values[i]), MidpointRounding.AwayFromZero)));
        }

        var projectedRevenue = daily.Sum(p => p.Revenue);

        // Compared against the equally-long tail of actuals, not against the whole range: "+18%"
        // has to mean "than the last <horizon> days" or the number is comparing a fortnight to a
        // year. Null when that tail sold nothing — the Prodaja tab drops the same line for the same
        // reason — and null when the range is shorter than the horizon, because then there is no
        // equally-long tail to compare against and the honest answer is to say nothing rather than
        // to report a projected year as +1.000% of the month it was fitted on.
        var tail = history.Count >= horizon ? history.TakeLast(horizon).Sum(p => p.Revenue) : (decimal?)null;
        var changePercent = tail is null or 0m
            ? (decimal?)null
            : Round2((projectedRevenue - tail.Value) / tail.Value * 100m);

        var bucketDays = BucketDays(horizon);

        return new ForecastBlock(
            Source: source,
            Horizon: horizon,
            ProjectedRevenue: Round2(projectedRevenue),
            ProjectedSold: daily.Sum(p => p.Sold),
            ChangePercent: changePercent,
            // The tail the projection continues, so the client draws one unbroken series. Capped at
            // the horizon's own length so the chart never becomes mostly history.
            Actual: Bucket(
                [
                    .. history.TakeLast(horizon).Select(p => new ForecastPoint(
                        p.Date, ReportSeries.DayLabel(p.Date), Round2(p.Revenue), Round2(p.Revenue), Round2(p.Revenue), p.Sold))
                ],
                bucketDays,
                anchorAtEnd: true),
            Points: Bucket(daily, bucketDays, anchorAtEnd: false));
    }

    /// <summary>
    /// How many days one drawn point covers, so a chart of any horizon comes out around a dozen
    /// bars a side.
    ///
    /// A month stays daily — the weekly rhythm is the whole reason to look at a month, and it
    /// disappears the moment the days are pooled. Past that the rhythm is not what is being asked
    /// about, and a year drawn as 365 bars is 16,000px of horizontal scroll: nobody reads bar 200.
    /// </summary>
    internal static int BucketDays(int horizon) => horizon switch
    {
        <= 31 => 1,
        <= 100 => 7,
        <= 200 => 14,
        _ => 30
    };

    /// <summary>
    /// Folds daily points into the points the chart draws. A pass-through when
    /// <paramref name="bucketDays"/> is 1, which is the one-month horizon.
    ///
    /// <paramref name="anchorAtEnd"/> decides where the short bucket lands when the series does not
    /// divide evenly. A projection is anchored at its first day and runs as far as the horizon
    /// reaches, so its remainder falls at the far end; the tail of actuals is anchored at its *last*
    /// day — the join with the projection — so its remainder falls at the start, on the oldest bar.
    /// Anchoring both at the start would put a half-height stub exactly at the join and invent a
    /// slump there that the data does not contain.
    /// </summary>
    private static List<ForecastPoint> Bucket(List<ForecastPoint> days, int bucketDays, bool anchorAtEnd)
    {
        if (bucketDays <= 1 || days.Count == 0) return days;

        var buckets = new List<ForecastPoint>((days.Count / bucketDays) + 1);
        var remainder = days.Count % bucketDays;
        var index = 0;

        // The first bucket is the short one when the series is anchored at its end; every bucket
        // after it is a full one.
        var length = anchorAtEnd && remainder > 0 ? remainder : bucketDays;

        while (index < days.Count)
        {
            var take = Math.Min(length, days.Count - index);
            var slice = days.GetRange(index, take);

            buckets.Add(new ForecastPoint(
                Date: slice[0].Date,
                Label: ReportSeries.DayLabel(slice[0].Date),
                Revenue: Round2(slice.Sum(p => p.Revenue)),
                // The bounds are summed with the values they belong to: a bucket's band is the band
                // of the days inside it, which is what the legend already states as one total.
                LowerBound: Round2(slice.Sum(p => p.LowerBound)),
                UpperBound: Round2(slice.Sum(p => p.UpperBound)),
                Sold: slice.Sum(p => p.Sold)));

            index += take;
            length = bucketDays;
        }

        return buckets;
    }

    private (Projection Values, AnalyticsSource Source) Project(
        float[] series, IReadOnlyList<DailyPoint> history, int horizon)
    {
        if (CanFitModel(series.Length, horizon))
        {
            var fitted = TryFitSsa(series, horizon);
            if (fitted is not null)
                return (fitted.Value, AnalyticsSource.Model);
        }

        return (Heuristic(series, history, horizon), AnalyticsSource.Heuristic);
    }

    /// <summary>
    /// Whether SSA is allowed to answer a horizon this long from a history this short.
    ///
    /// Two conditions, and the second is the one the longer horizons added. A model may not project
    /// further than it has seen: asked for a year from a month of history, SSA does not fail — it
    /// extrapolates the month's weekly component 365 times and returns a confident straight line
    /// with a 95% band around it, which is exactly the artefact the horizon allow-list was written
    /// to keep out. Refusing here drops that request to the heuristic rung instead, where the UI
    /// prints "procjena na osnovu prosjeka" and the reader is told what they are looking at.
    ///
    /// The consequence is worth stating plainly: a one-year projection is only ever fitted when the
    /// selected range is itself about a year. That is the honest constraint, not a limitation to
    /// work around.
    /// </summary>
    internal static bool CanFitModel(int points, int horizon)
        => points >= MinPointsForModel && horizon <= points;

    /// <summary>
    /// Fits SSA and returns null rather than throwing if it cannot.
    ///
    /// The failure that actually happens is a degenerate series — an all-zero or near-constant
    /// stretch, which is a perfectly ordinary thing for a small organizer's month to be — and SSA
    /// answers it with a singular matrix rather than a graceful result. Catching here is what turns
    /// that into the heuristic rung of the ladder instead of a 500 on a report the user is entitled
    /// to see.
    /// </summary>
    private Projection? TryFitSsa(float[] series, int horizon)
    {
        try
        {
            var mlContext = new MLContext(seed: 0);
            var data = mlContext.Data.LoadFromEnumerable(series.Select(v => new SeriesRecord { Value = v }));

            var pipeline = mlContext.Forecasting.ForecastBySsa(
                outputColumnName: nameof(SeriesForecast.Forecast),
                inputColumnName: nameof(SeriesRecord.Value),
                windowSize: WindowSize,
                seriesLength: series.Length,
                trainSize: series.Length,
                horizon: horizon,
                confidenceLevel: 0.95f,
                confidenceLowerBoundColumn: nameof(SeriesForecast.LowerBound),
                confidenceUpperBoundColumn: nameof(SeriesForecast.UpperBound));

            // Fit, engine construction and prediction all inside the gate: they are one indivisible
            // use of ML.NET's time-series machinery, and splitting them would leave exactly the race
            // MlGate exists to close.
            var prediction = MlGate.Run(() =>
            {
                var engine = pipeline.Fit(data).CreateTimeSeriesEngine<SeriesRecord, SeriesForecast>(mlContext);
                return engine.Predict();
            });

            if (prediction.Forecast.Length < horizon || prediction.Forecast.Any(float.IsNaN))
                return null;

            return new Projection(prediction.Forecast, prediction.LowerBound, prediction.UpperBound);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "SSA prognoza nije uspjela na seriji od {Points} tačaka — koristi se heuristika.", series.Length);
            return null;
        }
    }

    /// <summary>
    /// Moving average × weekday index: the level from the last fortnight, the shape from how each
    /// weekday has historically compared to the overall mean.
    ///
    /// Crude on purpose. It exists for the case where there is too little history to fit anything,
    /// and its whole job is to be defensible rather than clever — a Saturday is projected above a
    /// Tuesday because Saturdays have been above Tuesdays, and nothing more is claimed. The index
    /// is clamped so a single freak day cannot triple a projection, and it falls back to 1.0 for
    /// any weekday seen fewer than twice.
    /// </summary>
    private static Projection Heuristic(float[] series, IReadOnlyList<DailyPoint> history, int horizon)
    {
        var level = series.TakeLast(Math.Min(series.Length, 14)).DefaultIfEmpty(0f).Average();
        var overallMean = series.DefaultIfEmpty(0f).Average();

        var byWeekday = history
            .Select((point, index) => (point.Date.DayOfWeek, Value: series[index]))
            .GroupBy(x => x.DayOfWeek)
            .ToDictionary(g => g.Key, g => (Count: g.Count(), Mean: g.Average(x => x.Value)));

        float IndexFor(DayOfWeek day)
        {
            if (overallMean <= 0f || !byWeekday.TryGetValue(day, out var stats) || stats.Count < 2)
                return 1f;

            return Math.Clamp(stats.Mean / overallMean, 0.5f, 2f);
        }

        // One standard deviation of the series, widened to a 95% band. Wider than SSA's interval
        // would be, which is the honest outcome: less history means less certainty, and the band
        // should say so.
        var variance = series.Length < 2
            ? 0f
            : series.Sum(v => (v - overallMean) * (v - overallMean)) / (series.Length - 1);
        var margin = 1.96f * MathF.Sqrt(variance);

        var lastDate = history[^1].Date;
        var values = new float[horizon];
        var lower = new float[horizon];
        var upper = new float[horizon];

        for (var i = 0; i < horizon; i++)
        {
            var value = level * IndexFor(lastDate.AddDays(i + 1).DayOfWeek);
            values[i] = value;
            lower[i] = value - margin;
            upper[i] = value + margin;
        }

        return new Projection(values, lower, upper);
    }

    /// <summary>Revenue cannot be negative, and an SSA extrapolation of a declining series
    /// routinely goes below zero a week or two out. Clamped rather than left to render a
    /// "-340,00 KM" bar the organizer has to mentally discard.</summary>
    private static decimal NonNegative(float value) => (decimal)Math.Max(0f, value);

    private static ForecastBlock Empty(int horizon, AnalyticsSource source) =>
        new(source, horizon, 0m, 0, null, [], []);

    private readonly record struct Projection(float[] Values, float[] Lower, float[] Upper);
}
