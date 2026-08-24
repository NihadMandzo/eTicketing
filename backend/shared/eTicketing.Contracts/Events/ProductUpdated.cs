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

/// <summary><paramref name="Field"/> is the Bosnian display label ("Datum i vrijeme", "Grad", ...),
/// resolved in Catalog where the domain meaning lives, so eTicketing.Notifications stays a dumb
/// renderer with no field-name mapping table of its own.</summary>
public record ProductFieldChange(string Field, string? OldValue, string? NewValue);
