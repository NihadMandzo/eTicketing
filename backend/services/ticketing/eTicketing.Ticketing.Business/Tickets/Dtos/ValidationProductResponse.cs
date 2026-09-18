using eTicketing.Contracts.Pagination;
using eTicketing.Contracts.Persistence;

namespace eTicketing.Ticketing.Business.Tickets;

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
