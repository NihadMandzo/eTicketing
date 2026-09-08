using eTicketing.Ticketing.Data.Entities;

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

/// <summary>Prices the held reservation server-side and asks eTicketing.Payment for the intent the
/// buyer will confirm. Runs exactly the same validation POST /purchases does, so an expired hold or
/// a bad line item is caught before any payment object exists.</summary>
public record CreatePaymentIntentRequest
{
    public string HoldId { get; init; } = string.Empty;
    public IReadOnlyList<PurchaseLineItemRequest> LineItems { get; init; } = [];
}

/// <param name="Provider">"Mock" or "Stripe". The clients branch on this to render Stripe Elements
/// or the mock card form, which is what lets a provider switch take effect with no rebuild.</param>
/// <param name="Amount">Authoritative, computed here from the held sector and its ticket types. The
/// client never states a price.</param>
public record PurchaseIntentResponse(
    string Provider,
    string? PublishableKey,
    Guid OrderId,
    string IntentId,
    string? ClientSecret,
    decimal Amount,
    string Currency,
    bool IsSubscription);

public record PurchaseLineItemRequest
{
    // Null iff the held Sector has no TicketTypes (today's single-implicit-price path).
    public Guid? TicketTypeId { get; init; }
    public int Quantity { get; init; }
}

public record PurchaseResponse(
    Guid OrderId,
    Guid ProductId,
    Guid SectorId,
    decimal TotalPaid,
    DateTime PurchasedAt,
    IReadOnlyList<TicketResponse> Tickets);

/// <param name="QrPayload">The signed code this ticket's QR encodes. Shown to the holder as a
/// fallback the gate can type in, and the exact string a scanner reads back.</param>
/// <param name="QrImage">A ready-to-render <c>data:image/png;base64,...</c> QR. Rendered here
/// rather than in each client so web and mobile need no QR library of their own — see
/// TicketQrImage.</param>
public record TicketResponse(
    Guid Id,
    Guid OrderId,
    Guid SectorId,
    string SectorName,
    Guid ProductId,
    Guid? TicketTypeId,
    string? TicketTypeName,
    TicketStatus Status,
    decimal PricePaid,
    DateOnly? ValidDate,
    DateOnly? ValidFrom,
    DateOnly? ValidTo,
    DateTime CreatedAt,
    string QrPayload,
    string QrImage,
    /// <summary>RecurringReservation only: whether the holder is currently inside, per the gate's
    /// entry/exit toggle (see TicketValidationService). Always false for the one-shot modes, which
    /// are spent by their single scan rather than tracked in and out.</summary>
    bool IsInside = false);
