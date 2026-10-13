namespace eTicketing.Ticketing.Business.Sectors;

/// <param name="CounterMissing">True when the hold was refused only because Redis has no counter
/// for this sector/date at all — a brand-new counter, or one Redis lost. Not a "sold out": the
/// caller rebuilds the counter from the database and tries again (see CapacityHoldRecovery).</param>
public record HoldResult(bool Success, string? HoldId, DateTime? ExpiresAt, bool CounterMissing = false);
