using eTicketing.Ticketing.Business.Time;
using eTicketing.Ticketing.Data.Repositories;

namespace eTicketing.Ticketing.Business.Reports;

/// <summary>Turning UTC-hour repository rows into the labelled local-day series that every report
/// and the analytics blocks share.</summary>
internal static class ReportSeries
{
    /// <summary>Bosnian short month names, matching the desktop app's own formatting and the
    /// labels in docs/Design/Reports.dc.html.</summary>
    public static readonly string[] MonthNames =
        ["jan", "feb", "mar", "apr", "maj", "jun", "jul", "avg", "sep", "okt", "nov", "dec"];

    /// <summary>
    /// Folds UTC-hour rows into local calendar days.
    ///
    /// An hour is the finest grain that survives the conversion unambiguously: DST transitions
    /// land on hour boundaries, so every row belongs to exactly one local date. Because
    /// ReportRange.Create builds the SQL window as exactly [local From 00:00, local To+1 00:00),
    /// every key produced here is guaranteed to fall inside [range.From, range.To] — which is what
    /// lets BuildBuckets visit all of them.
    /// </summary>
    public static Dictionary<DateOnly, LocalDaySales> ToLocalDays(
        List<HourlySalesFacts> hourly, PlatformClock clock)
    {
        var byDay = new Dictionary<DateOnly, LocalDaySales>();

        foreach (var row in hourly)
        {
            var day = clock.LocalDateOf(row.HourUtc);
            byDay.TryGetValue(day, out var running);
            byDay[day] = new LocalDaySales(
                running.Sold + row.Sold,
                running.Revenue + row.Revenue,
                running.Cancelled + row.Cancelled,
                running.CancelledAmount + row.CancelledAmount,
                running.OnlineSold + row.OnlineSold,
                running.PrintedSold + row.PrintedSold);
        }

        return byDay;
    }

    /// <summary>
    /// Every day of the range in order, zero-filled where nothing sold.
    ///
    /// The forecaster and the anomaly detector both need a gap-free series: a time-series model
    /// reads position as time, so silently dropping a day nobody bought anything on would pull
    /// every later point one day earlier and corrupt the weekly seasonality the model is fitted on.
    /// The tab charts can tolerate gaps because BuildBuckets walks the range too — this is the same
    /// guarantee, exposed one row per day.
    /// </summary>
    public static List<DailyPoint> ToDailySeries(
        ReportRange range, Dictionary<DateOnly, LocalDaySales> byDay)
    {
        var series = new List<DailyPoint>(range.Days);
        for (var day = range.From; day <= range.To; day = day.AddDays(1))
        {
            byDay.TryGetValue(day, out var sales);
            series.Add(new DailyPoint(day, sales.Revenue, sales.Sold));
        }

        return series;
    }

    /// <summary>
    /// Folds the per-day rows into the chart's bars. Buckets are generated from the range itself
    /// rather than from the rows, so a week nobody bought anything in is drawn as a zero-height bar
    /// instead of vanishing and silently compressing the timeline.
    /// </summary>
    public static List<ReportBucket> BuildBuckets(ReportRange range, Dictionary<DateOnly, LocalDaySales> byDay)
    {
        var buckets = new List<ReportBucket>();

        // Week and month buckets are anchored at the start of the range and walk forward, so the
        // first bar is always the range's own start date rather than a partial period carved
        // backwards from today.
        var cursor = range.From;
        while (cursor <= range.To)
        {
            var end = range.Unit switch
            {
                ReportBucketUnit.Day => cursor,
                ReportBucketUnit.Week => cursor.AddDays(6),
                _ => new DateOnly(cursor.Year, cursor.Month, 1).AddMonths(1).AddDays(-1)
            };
            if (end > range.To) end = range.To;

            decimal revenue = 0;
            var sold = 0;
            for (var day = cursor; day <= end; day = day.AddDays(1))
            {
                if (!byDay.TryGetValue(day, out var facts)) continue;
                revenue += facts.Revenue;
                sold += facts.Sold;
            }

            buckets.Add(new ReportBucket(Label(cursor, range.Unit), revenue, sold));
            cursor = end.AddDays(1);
        }

        return buckets;
    }

    public static string Label(DateOnly start, ReportBucketUnit unit) => unit switch
    {
        ReportBucketUnit.Month => char.ToUpperInvariant(MonthNames[start.Month - 1][0]) + MonthNames[start.Month - 1][1..],
        _ => $"{start.Day}. {MonthNames[start.Month - 1]}"
    };

    /// <summary>Day label, always — the forecast is drawn in days no matter which unit the tab
    /// chart happens to be bucketed in.</summary>
    public static string DayLabel(DateOnly date) => Label(date, ReportBucketUnit.Day);
}
