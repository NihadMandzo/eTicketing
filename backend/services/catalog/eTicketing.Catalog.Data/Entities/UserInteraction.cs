using eTicketing.Contracts.Persistence;

namespace eTicketing.Catalog.Data.Entities;

/// <summary>
/// One buyer's relationship with one product, as a single row per (UserId, ProductId, Type) —
/// deliberately NOT one row per event. A user who opens the same product forty times must not
/// write forty rows; the repeat bumps <see cref="Count"/> instead. That keeps the table bounded by
/// users × products × 2 rather than by raw traffic, and hands the recommender a natural confidence
/// weight for free: a product viewed ten times is a stronger signal than one viewed once.
///
/// This is the only place Catalog stores anything about a buyer, and it stores the minimum that
/// makes a recommendation possible — never money, order ids, or ticket data, all of which stay in
/// eTicketing.Ticketing where they belong.
/// </summary>
public class UserInteraction : BaseEntity
{
    public Guid Id { get; set; }

    // Identity owns User in a separate database/microservice — plain Guid column, deliberately no
    // FK/navigation across the service boundary (same convention as Product.OrganizationId).
    public Guid UserId { get; set; }

    // Product lives in THIS database, so unlike UserId this one is a real FK with a navigation and
    // cascade delete: deleting a product must take its interaction history with it (hard delete
    // only, per .claude/rules/00-workflow-and-testing.md).
    public Guid ProductId { get; set; }
    public Product? Product { get; set; }

    public InteractionType Type { get; set; }

    /// <summary>How many times this (user, product, type) pair has occurred. Starts at 1 on the
    /// row's first write and is incremented in place afterwards.</summary>
    public int Count { get; set; }

    public DateTime LastOccurredAt { get; set; }

    /// <summary>Weight handed to the matrix-factorization trainer. A purchase is worth five views:
    /// a view is a browse, a purchase is a commitment, and without the multiplier the far more
    /// numerous views would drown out the signal that actually matters.</summary>
    public const int PurchaseWeightMultiplier = 5;
}
