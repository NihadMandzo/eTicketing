using eTicketing.Contracts.Pagination;
using eTicketing.Contracts.Persistence;
using eTicketing.Ticketing.Data.Entities;

namespace eTicketing.Ticketing.Data.Repositories;

/// <summary>Projection, not an entity — one row per (product, mode) with today's ticket tallies.</summary>
public record TicketValidationCounts(Guid ProductId, TicketingMode TicketingMode, int TotalToday, int ValidatedToday);
