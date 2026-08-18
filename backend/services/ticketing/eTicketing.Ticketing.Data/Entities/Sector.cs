using eTicketing.Contracts.Persistence;

namespace eTicketing.Ticketing.Data.Entities;

/// <summary>
/// The generic capacity unit of a Product (owned by eTicketing.Catalog): a seating/capacity
/// section for a SingleOccurrence event (e.g. "VIP"), a flat per-day capacity+price for a
/// DailyEntry month (e.g. "August 2026"), or one specific labeled space for a
/// RecurringReservation product (e.g. "A-12"). Behavior is driven by TicketingMode, copied from
/// the owning Product's Category at creation time.
/// </summary>
public class Sector : BaseEntity
{
    public Guid Id { get; set; }

    // Cross-service ref to Catalog.Product — plain Guid column, no FK/navigation across the
    // service boundary, same convention as Product.OrganizationId in eTicketing.Catalog.
    public Guid ProductId { get; set; }

    // Denormalized from the owning Product at Sector-creation time (same internal Catalog call
    // that resolves TicketingMode below) so ownership checks and "my sectors" listings never need
    // a second cross-service call after creation.
    public Guid OrganizationId { get; set; }

    public string Name { get; set; } = string.Empty;

    // SingleOccurrence: total seats. DailyEntry: capacity PER DAY within PeriodYear/PeriodMonth.
    // RecurringReservation: always 1 (one specific space).
    public int Capacity { get; set; }

    // SingleOccurrence: price per ticket. DailyEntry: price per day-ticket. RecurringReservation:
    // price per billing period (month).
    public decimal Price { get; set; }

    public PublishStatus Status { get; set; } = PublishStatus.Draft;

    // Denormalized copy of the owning Product's Category.TicketingMode, captured once via the
    // internal Ticketing→Catalog call (ICatalogClient) at Sector-creation time. Lets hold/purchase
    // logic branch by mode without a synchronous cross-service call on the purchase-critical path.
    public TicketingMode TicketingMode { get; set; }

    // DailyEntry only: which calendar month this flat capacity+price row covers. Null otherwise.
    public int? PeriodYear { get; set; }
    public int? PeriodMonth { get; set; } // 1-12
}
