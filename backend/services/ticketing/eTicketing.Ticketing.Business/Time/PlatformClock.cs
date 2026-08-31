using Microsoft.Extensions.Options;

namespace eTicketing.Ticketing.Business.Time;

/// <summary>
/// "What day is it here?" — the platform's local business day, not UTC's.
///
/// Every date a buyer or an organizer ever picks is a local wall-clock date: Ticket.ValidDate,
/// Ticket.ValidFrom/ValidTo and Product.Date are plain DateOnly/DateTime values carrying no offset,
/// chosen as "the day I want". Deriving today from UtcNow compares them against the wrong day for
/// the 1-2 hours between local midnight (UTC+1 winter, UTC+2 summer) and UTC midnight — every
/// night, deterministically, which is long enough to turn a valid ticket away at the gate.
///
/// The split to keep in mind when adding callers: instants stay UTC — <see cref="UtcNow"/> is what
/// timestamps like Ticket.ValidatedAt use. Only the calendar-day question goes through
/// <see cref="Today"/>.
/// </summary>
public sealed class PlatformClock
{
    private readonly TimeProvider _timeProvider;
    private readonly TimeZoneInfo _timeZone;

    /// <summary>Resolves the zone once, at construction. An unknown id throws here rather than
    /// being swallowed: a container missing tzdata should fail loudly at startup instead of booting
    /// on UTC and silently reintroducing the very bug this class exists to prevent.</summary>
    public PlatformClock(TimeProvider timeProvider, IOptions<PlatformTimeOptions> options)
    {
        _timeProvider = timeProvider;
        _timeZone = TimeZoneInfo.FindSystemTimeZoneById(options.Value.TimeZoneId);
    }

    /// <summary>The current instant, in UTC — for storing timestamps, never for "is this today".</summary>
    public DateTime UtcNow => _timeProvider.GetUtcNow().UtcDateTime;

    /// <summary>Today's calendar date in the platform's local time zone.</summary>
    public DateOnly Today() =>
        DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(UtcNow, _timeZone));

    /// <summary>
    /// The UTC instant at which the given local calendar day begins — the bridge the reports need
    /// between a date somebody picked in a date field and the UTC <c>CreatedAt</c> column those
    /// dates have to be compared against.
    ///
    /// The inverse direction of <see cref="Today"/>, and needed for the same reason: a report for
    /// "1. avgust" must start at 1 August 00:00 *here*, which is 31 July 22:00 UTC in summer. Doing
    /// this with <c>DateTime.SpecifyKind(..., Utc)</c> instead would quietly shift every range by
    /// the offset and drop the first two hours of every report's first day into the previous one.
    ///
    /// A local midnight that does not exist (the spring-forward hour, in zones where the
    /// transition happens at midnight) is resolved by <see cref="TimeZoneInfo.ConvertTimeToUtc"/>
    /// to the offset in force before the jump, which is the correct start of that business day.
    /// </summary>
    public DateTime ToUtcStartOfDay(DateOnly localDate) =>
        TimeZoneInfo.ConvertTimeToUtc(
            DateTime.SpecifyKind(localDate.ToDateTime(TimeOnly.MinValue), DateTimeKind.Unspecified),
            _timeZone);

    /// <summary>
    /// The local wall-clock time of a stored UTC instant — the direction the reports need when
    /// they turn <c>Ticket.CreatedAt</c> or <c>Ticket.ValidatedAt</c> back into "which day was
    /// that here" / "what time did they actually arrive".
    ///
    /// This exists so no caller has to hold a <see cref="TimeZoneInfo"/> of its own. In
    /// particular the repositories cannot: eTicketing.Ticketing.Data references only
    /// eTicketing.Contracts (Business → Data, never the reverse), so it has no way to see this
    /// class at all. That is why every reporting aggregation returns UTC-keyed rows and
    /// ReportService — which does hold a clock — decides what a local day or hour is.
    /// </summary>
    public DateTime ToLocal(DateTime utc) => TimeZoneInfo.ConvertTimeFromUtc(utc, _timeZone);

    /// <summary>The local calendar date a stored UTC instant falls on.</summary>
    public DateOnly LocalDateOf(DateTime utc) => DateOnly.FromDateTime(ToLocal(utc));
}
