namespace eTicketing.Ticketing.Business.Tickets;

/// <summary>
/// Serializes gate validation of one specific ticket across every scanner in the building. Without
/// it, two organizers scanning the same QR within milliseconds of each other both read
/// Status=Confirmed, both write Status=Used, and both see a green VALIDNA card — one person gets in
/// on someone else's ticket.
///
/// Deliberately a separate abstraction from <see cref="Sectors.ISectorCapacityLock"/> even though
/// both are Redis: that one is a capacity counter with lazy expiry semantics, this one is a plain
/// short-lived mutex. Concrete implementation (RedisTicketValidationLock) lives in
/// .Api/Infrastructure/Redis per .claude/rules/10-backend.md.
/// </summary>
public interface ITicketValidationLock
{
    /// <summary>Returns a release token on success, or null if another scan currently holds the
    /// lock for this ticket. The token must be passed back to <see cref="ReleaseAsync"/> — it's
    /// what stops a slow caller from releasing a lock a later scan already acquired.</summary>
    Task<string?> TryAcquireAsync(Guid ticketId, CancellationToken ct = default);

    Task ReleaseAsync(Guid ticketId, string token, CancellationToken ct = default);
}
