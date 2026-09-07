namespace eTicketing.Ticketing.Business.Sectors;

public record HoldResult(bool Success, string? HoldId, DateTime? ExpiresAt);

/// <summary>What a holdId actually reserved, reconstructed server-side from Redis. PurchaseService
/// resolves SectorId/Quantity exclusively from this — never from client-supplied values — so a
/// caller cannot present one sector's hold together with a different sector/quantity claim and
/// have capacity silently not decremented for what was actually sold.</summary>
public record HeldReservation(Guid SectorId, DateOnly? Date, int Quantity);

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

    /// <summary>Reads back what a still-active holdId reserved, without mutating anything. Returns
    /// null if the hold is unknown/already expired.</summary>
    Task<HeldReservation?> PeekAsync(string holdId, CancellationToken ct = default);

    /// <summary>Purchase succeeded: the decrement becomes permanent (TTL cancelled). No-op if
    /// the hold already expired.</summary>
    Task ConfirmAsync(string holdId, CancellationToken ct = default);

    /// <summary>Returns the held quantity back to available capacity. Works only while the hold is
    /// still pending: it finds the counter through the holdinfo pointer, which
    /// <see cref="ConfirmAsync"/> deletes. To release capacity that was already confirmed, use
    /// <see cref="ReleaseConfirmedAsync"/>.</summary>
    Task ReleaseAsync(string holdId, CancellationToken ct = default);

    /// <summary>
    /// Frees capacity that <see cref="ConfirmAsync"/> already made permanent — today, a cancelled
    /// RecurringReservation subscription giving its parking space back.
    ///
    /// Separate from <see cref="ReleaseAsync"/> because it cannot go through the holdinfo pointer:
    /// ConfirmAsync deletes that on purpose, so a replayed purchase cannot resolve the same hold
    /// twice. The sector and date therefore have to be supplied by the caller, and the hold id comes
    /// from Subscription.CapacityHoldId, which exists precisely so this call is possible months after
    /// the purchase.
    ///
    /// Idempotent: releasing an unknown or already-released hold does nothing.
    /// </summary>
    Task ReleaseConfirmedAsync(Guid sectorId, DateOnly? date, string holdId, CancellationToken ct = default);

    /// <summary>How many admissions are still available on this counter, without reserving any of
    /// them. Read-only and therefore inherently a snapshot: a concurrent buyer can take the last
    /// seat between this call and whatever the caller does next, which is exactly why
    /// <see cref="TryHoldAsync"/> re-checks atomically rather than trusting a number read here.
    /// Used to show an organizer how many physical tickets a sector still has room for.</summary>
    Task<int> GetRemainingAsync(Guid sectorId, int capacity, DateOnly? date, CancellationToken ct = default);
}
