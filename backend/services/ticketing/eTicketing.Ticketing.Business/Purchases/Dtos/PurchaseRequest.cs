namespace eTicketing.Ticketing.Business.Purchases;

/// <summary>
/// Completes a purchase whose payment the buyer has already confirmed in their browser.
///
/// No card data, by design: with the Stripe provider the card goes straight from the browser to
/// Stripe and only identifiers come back, so nothing card-shaped ever reaches this service. That is
/// also why the fields below are not enough on their own to buy anything -- OrderId and
/// PaymentIntentId are re-verified against the provider's own metadata before a single unit is
/// captured (see eTicketing.Payment's gateway), so echoing back someone else's identifiers fails.
/// </summary>
public record PurchaseRequest
{
    public string HoldId { get; init; } = string.Empty;
    public IReadOnlyList<PurchaseLineItemRequest> LineItems { get; init; } = [];

    /// <summary>Minted server-side by POST /purchases/payment-intent and echoed back here.</summary>
    public Guid OrderId { get; init; }

    /// <summary>The provider intent the buyer confirmed, or the subscription reference on the
    /// RecurringReservation path.</summary>
    public string PaymentIntentId { get; init; } = string.Empty;

    /// <summary>
    /// Mock provider only: the last four digits of the demo card, where "0000" simulates a decline
    /// (see MockPaymentGateway). It exists so PAYMENT_PROVIDER=Mock stays a working fallback with a
    /// real declined-card path, per docs/arhitektura-migracija-mikroservisi-eda.md section 9. The
    /// Stripe provider ignores it, and it is never enough to charge anything.
    /// </summary>
    public string? SimulatedLast4 { get; init; }
}
