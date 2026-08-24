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
}

/// <summary>
/// Deliberately returned as a SUCCESSFUL Result even when <paramref name="IsValid"/> is false. A
/// ticket that's already been used, belongs to another event, or expired is an expected, displayable
/// answer — the scanner has to paint a red card with the reason, which it can't do if the transport
/// swallowed it as a 404. Genuine failures (caller isn't an organizer → 403, lost the Redis lock →
/// 409) stay Result.Failure.
/// </summary>
public sealed record TicketValidationResponse(
    bool IsValid,
    string Code,
    string Message,
    Guid? TicketId,
    string? SectorName,
    string? TicketTypeName,
    string? HolderEmail,
    DateTime? ValidatedAt);
