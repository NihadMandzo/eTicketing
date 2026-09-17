using eTicketing.Ticketing.Business.Time;
using eTicketing.Ticketing.Data.Repositories;

namespace eTicketing.Ticketing.Business.Reports;

/// <summary>
/// A validated report range, resolved once into every form the pipeline needs: the local dates for
/// labels, the half-open UTC window for SQL, and the bucket unit the chart is drawn in.
///
/// Shared by ReportService and AnalyticsService — the AI Uvidi tab forecasts the very series the
/// Prodaja chart draws, so the two must resolve a range identically or the forecast would continue
/// a line the user is not looking at.
/// </summary>
internal readonly record struct ReportRange(
    DateOnly From, DateOnly To, DateTime FromUtc, DateTime ToUtcExclusive, PlatformClock Clock)
{
    public int Days => To.DayNumber - From.DayNumber + 1;

    public static ReportRange Create(DateOnly from, DateOnly to, PlatformClock clock) => new(
        from,
        to,
        clock.ToUtcStartOfDay(from),
        // Exclusive upper bound at the start of the day *after* To, so the whole of the last
        // day is included without any 23:59:59.999 rounding games.
        clock.ToUtcStartOfDay(to.AddDays(1)),
        clock);

    /// <summary>Any request carrying a range — ReportQuery, ReportExportQuery or InsightsQuery.</summary>
    public static ReportRange Create(IReportRange query, PlatformClock clock)
        => Create(query.From, query.To, clock);

    /// <summary>The equal-length range immediately before this one — the growth comparison
    /// baseline.</summary>
    public ReportRange Preceding() => Create(From.AddDays(-Days), From.AddDays(-1), Clock);

    /// <summary>Daily bars up to a fortnight, weekly up to a quarter, monthly beyond. Chosen
    /// here rather than by the client so the PDF and all three frontends can never disagree
    /// about what a bar means.</summary>
    public ReportBucketUnit Unit => Days switch
    {
        <= 14 => ReportBucketUnit.Day,
        <= 92 => ReportBucketUnit.Week,
        _ => ReportBucketUnit.Month
    };

    public ReportPeriod ToPeriod() => new(From, To, Days, Unit);
}
