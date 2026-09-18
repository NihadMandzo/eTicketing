namespace eTicketing.Ticketing.Business.Tickets;

/// <summary>
/// Deliberately returned as a SUCCESSFUL Result even when <paramref name="IsValid"/> is false. A
/// ticket that's already been used, belongs to another event, or expired is an expected, displayable
/// answer — the scanner has to paint a red card with the reason, which it can't do if the transport
/// swallowed it as a 404. Genuine failures (caller isn't an organizer → 403, lost the Redis lock →
/// 409) stay Result.Failure.
/// </summary>
/// <param name="Direction">What this particular scan did. <see cref="GatePassageDirection.Entry"/>
/// for every admitted one-shot ticket and for a subscription holder coming in;
/// <see cref="GatePassageDirection.Exit"/> for a subscription holder going out — a valid scan that
/// must NOT read as "admitted" on the scanner's green card.</param>
/// <param name="IsInside">RecurringReservation only: whether the holder is inside after this scan.
/// Always false for the one-shot modes, which have no such state.</param>
public sealed record TicketValidationResponse(
    bool IsValid,
    string Code,
    string Message,
    Guid? TicketId,
    string? SectorName,
    string? TicketTypeName,
    string? HolderEmail,
    DateTime? ValidatedAt,
    GatePassageDirection Direction = GatePassageDirection.None,
    bool IsInside = false);
