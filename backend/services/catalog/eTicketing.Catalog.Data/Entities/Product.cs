using eTicketing.Contracts.Persistence;

namespace eTicketing.Catalog.Data.Entities;

/// <summary>
/// The generic "thing an organizer creates and sells" — a classic one-time event, a museum
/// day-pass, a monthly parking space, or any future kind of ticketed offering. Behavior is
/// driven by the owning Category's TicketingMode, not by a type field on Product itself, so new
/// business categories can reuse an existing mode with zero code changes here.
/// </summary>
public class Product : BaseEntity
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;

    // Only meaningful/required when Category.TicketingMode == SingleOccurrence — the one-time
    // event's date/time shown on the public listing card. Null for DailyEntry/RecurringReservation:
    // DailyEntry's per-day availability lives on Sector (per month, in eTicketing.Ticketing) +
    // Ticket.ValidDate; RecurringReservation's cadence lives on Subscription.
    public DateTime? Date { get; set; }

    public int CategoryId { get; set; }
    public Category? Category { get; set; }

    // Organization lives in Identity's separate database/microservice — plain Guid column,
    // deliberately no FK/navigation across the service boundary.
    public Guid OrganizationId { get; set; }

    public PublishStatus Status { get; set; } = PublishStatus.Draft;

    // The exact map pin placed by the organizer when the product was created — required at the
    // request-validation layer (CreateProductRequestValidator), not nullable here: every Product
    // carries a real location regardless of TicketingMode (unlike Date, which is mode-conditional).
    public double Latitude { get; set; }
    public double Longitude { get; set; }

    // Coarse city, independent of the exact pin above — drives the server-side location filter on
    // GET /products* (a dropdown of cities is a much better filter UX than raw coordinates).
    public City City { get; set; }

    // Gallery photos live in Azure Blob Storage ("product-images" container), not as a column
    // here — see ProductImage. Managed exclusively through the dedicated POST/DELETE
    // /products/{id}/images endpoints, never bundled into Create/Update (same convention as
    // Category.IconBlobName / Organization.LogoBlobName).
    public ICollection<ProductImage> Images { get; set; } = new List<ProductImage>();
}
