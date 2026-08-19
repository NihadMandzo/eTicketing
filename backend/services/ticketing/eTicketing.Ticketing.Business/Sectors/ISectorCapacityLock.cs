namespace eTicketing.Ticketing.Business.Sectors;

public record HoldResult(bool Success, string? HoldId, DateTime? ExpiresAt);

/// <summary>
/// Redis-backed atomic capacity hold, generalized across TicketingModes via an optional date:
/// SingleOccurrence/RecurringReservation key off the sector alone
/// ("sector:{sectorId}:capacity"); DailyEntry keys off (sector, date)
/// ("sector:{sectorId}:date:{yyyy-MM-dd}:capacity") since capacity resets per calendar day.
/// Concrete implementation (RedisSectorCapacityLock) lives in .Api/Infrastructure/Redis — HTTP/
/// infra wiring is a hosting concern per .claude/rules/10-backend.md.
/// </summary>
public interface ISectorCapacityLock
{
    /// <summary>Atomically decrements the remaining count (lazily initialized to the sector's
    /// Capacity on first touch) by <paramref name="quantity"/> under a TTL. Returns
    /// Success=false (no hold created) if there isn't enough remaining capacity.</summary>
    Task<HoldResult> TryHoldAsync(Guid sectorId, int capacity, int quantity, DateOnly? date, TimeSpan ttl, CancellationToken ct = default);

    /// <summary>Purchase succeeded: the decrement becomes permanent (TTL cancelled). No-op if
    /// the hold already expired.</summary>
    Task ConfirmAsync(string holdId, CancellationToken ct = default);

    /// <summary>Returns the held quantity back to available capacity — used both for an
    /// abandoned-cart TTL expiry (handled automatically by Redis) and for an explicit business
    /// release (e.g. a cancelled RecurringReservation subscription freeing its space).</summary>
    Task ReleaseAsync(string holdId, CancellationToken ct = default);
}
