using eTicketing.Ticketing.Data.Entities;

namespace eTicketing.Ticketing.Business.Sectors;

/// <summary>
/// The database's answer to "how much of this capacity counter is taken", independent of Redis.
///
/// <para>Redis holds the live counter (see ISectorCapacityLock) because a hold has to be atomic and
/// fast, but Redis is not the record of what was sold — the Tickets and Subscriptions tables are.
/// When Redis loses a counter (a restart without persistence, a failover, a flush), the counter
/// must be rebuilt from here before the next hold, or every seat already sold looks free again and
/// the sector oversells.</para>
/// </summary>
public interface ISoldCapacityCounter
{
    /// <summary>Seats or spaces taken on this counter: non-cancelled tickets for
    /// SingleOccurrence (and for DailyEntry, on <paramref name="date"/>); non-cancelled
    /// subscriptions for RecurringReservation, whose space stays taken across every renewal.</summary>
    Task<int> CountAsync(Sector sector, DateOnly? date, CancellationToken ct = default);
}
