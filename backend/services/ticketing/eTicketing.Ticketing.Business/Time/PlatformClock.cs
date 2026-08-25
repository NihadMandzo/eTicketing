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
}
