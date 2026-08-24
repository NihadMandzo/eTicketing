using eTicketing.Contracts.Persistence;

namespace eTicketing.PdfGeneration.External;

/// <summary>Mirrors eTicketing.Catalog.Business.Products.ProductInternalResponse's JSON shape —
/// duplicated rather than shared across the service boundary, same convention as
/// eTicketing.Ticketing's own copy.</summary>
public record CatalogProductResponse(
    Guid Id,
    Guid OrganizationId,
    PublishStatus Status,
    TicketingMode TicketingMode,
    string Name,
    DateTime? Date,
    City City);

/// <summary>
/// TicketPurchased carries everything eTicketing.Ticketing knows, which is everything about the
/// tickets and nothing about the product they're for — Ticketing only stores a ProductId. The PDF
/// needs a real event name, date and city on it, so this is where they come from.
///
/// Safe to put on this path in a way it wouldn't be on the purchase critical path: this is an async
/// consumer, so a Catalog blip just means the message is retried a few seconds later, not a failed
/// checkout.
/// </summary>
public interface ICatalogClient
{
    Task<CatalogProductResponse?> GetProductAsync(Guid productId, CancellationToken ct = default);
}
