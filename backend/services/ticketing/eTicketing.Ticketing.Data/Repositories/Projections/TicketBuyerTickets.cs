namespace eTicketing.Ticketing.Data.Repositories;

/// <summary>A buyer plus the number of still-valid tickets they hold for one product.</summary>
public record TicketBuyerTickets(Guid UserId, string UserEmail, int TicketCount);

// ── Reporting projections ────────────────────────────────────────────────────────────────────
// Cancelled tickets are counted separately everywhere rather than folded into Sold/Revenue: a
// cancelled ticket was a real sale that was then undone, so the reports show both the gross
// figure and what came off it, never a silently netted number.
