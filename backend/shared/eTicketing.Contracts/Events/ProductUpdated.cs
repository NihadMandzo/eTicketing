namespace eTicketing.Contracts.Events;

/// <summary>
/// Published by eTicketing.Catalog when a PUBLISHED Product's vital fields change (name,
/// description, date/time, city, map coordinates, category). Draft edits publish nothing — a
/// draft has no buyers by construction. Consumed by eTicketing.Ticketing, the only service that
/// knows who holds a ticket for the product.
/// </summary>
public record ProductUpdated(
    Guid ProductId,
    string ProductName,
    DateTime UpdatedAt,
    IReadOnlyList<ProductFieldChange> Changes);
