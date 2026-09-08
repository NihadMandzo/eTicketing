using eTicketing.Contracts.Pagination;
using eTicketing.Contracts.Persistence;

namespace eTicketing.Ticketing.Business.Tickets;

public sealed record TicketQuery : BaseSearchObject;

/// <summary>One row of the organizer's "what am I validating today" list. Counts are for the
/// current calendar day only — a product with 400 tickets across the month shows only the ones
/// admitting entry today.</summary>
public sealed record ValidationProductResponse(
    Guid ProductId,
    string Name,
    DateTime? Date,
    TicketingMode TicketingMode,
    int TotalToday,
    int ValidatedToday);

/// <summary><paramref name="Code"/> is whatever the scanner read — a full signed QR payload, or a
/// bare ticket GUID typed into the manual-entry fallback. <paramref name="ProductId"/> is the
/// product the organizer opened the scanner for: validation is always "is this ticket good for
/// THIS product", never "is this ticket good in general".</summary>
public sealed record ValidateTicketRequest
{
    public Guid ProductId { get; init; }
    public string Code { get; init; } = string.Empty;

    /// <summary>Optional narrowing to specific sectors of <see cref="ProductId"/> — "this door only
    /// takes VIP and Loža". Null or empty keeps the original behavior of admitting any sector of
    /// the product, which is what the mobile scanner (which opens on a product, not a door) sends.
    /// A registered gate device does not use this field at all: its scope comes from its own row,
    /// never from the wire.</summary>
    public List<Guid>? SectorIds { get; init; }
}

/// <summary>Which way through the gate a scan moved the holder.
///
/// Persisted nowhere — it is a per-scan answer, not ticket state — but serialized as the integer
/// ordinal like every other enum on this wire, so append only.</summary>
public enum GatePassageDirection
{
    /// <summary>Nothing moved: a rejected scan, or a one-shot ticket, which has no direction to
    /// speak of. The default, so existing clients that ignore the field are unaffected.</summary>
    None,

    /// <summary>The holder was admitted.</summary>
    Entry,

    /// <summary>A RecurringReservation holder left, re-arming their ticket for the next entry.</summary>
    Exit
}

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
