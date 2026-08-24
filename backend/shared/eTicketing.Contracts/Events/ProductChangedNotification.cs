namespace eTicketing.Contracts.Events;

/// <summary>
/// Published by eTicketing.Ticketing, one per DISTINCT buyer email holding a live ticket for a
/// product that just changed (see <see cref="ProductUpdated"/>). A buyer holding three tickets
/// for the same product gets exactly one of these, not three.
/// </summary>
public record ProductChangedNotification(
    Guid ProductId,
    string ProductName,
    string RecipientEmail,
    IReadOnlyList<ProductFieldChange> Changes);
