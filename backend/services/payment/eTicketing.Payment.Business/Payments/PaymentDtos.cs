using eTicketing.Payment.Data.Entities;

namespace eTicketing.Payment.Business.Payments;

/// <summary>
/// Creates the provider-side object the buyer confirms in their browser. Called by
/// eTicketing.Ticketing only, over the internal Docker network -- this endpoint has no Gateway route.
/// </summary>
public record CreateIntentRequest
{
    /// <summary>Computed server-side by Ticketing from the held sector and its ticket types. The
    /// client never states a price.</summary>
    public decimal Amount { get; init; }

    /// <summary>The order this pays for, and the idempotency key: unique index on Payments.OrderRef,
    /// provider idempotency key, and the metadata a later capture is verified against.</summary>
    public string OrderRef { get; init; } = string.Empty;

    public Guid UserId { get; init; }
    public string? CustomerEmail { get; init; }
    public string Description { get; init; } = string.Empty;

    /// <summary>The Redis hold this order is for, recorded as provider metadata so an intent can be
    /// traced back to the reservation that justified it.</summary>
    public string HoldRef { get; init; } = string.Empty;

    public Guid SectorId { get; init; }

    /// <summary>True for a RecurringReservation sector: creates a real recurring subscription whose
    /// first invoice the client confirms, rather than a one-off intent.</summary>
    public bool IsSubscription { get; init; }

    /// <summary>Shown on the buyer's card statement and in the provider dashboard. Required when
    /// IsSubscription is true.</summary>
    public string? SubscriptionProductName { get; init; }
}

/// <param name="Provider">"Mock" or "Stripe". Returned rather than compiled into each client so the
/// frontends render the right payment form and a provider switch needs no rebuild.</param>
public record PaymentIntentResponse(
    string Provider,
    string? PublishableKey,
    string IntentId,
    string? ClientSecret,
    decimal Amount,
    string Currency,
    string? SubscriptionReference);

public record CapturePaymentRequest
{
    public string IntentId { get; init; } = string.Empty;
    public string OrderRef { get; init; } = string.Empty;
    public Guid UserId { get; init; }
    public decimal ExpectedAmount { get; init; }

    /// <summary>Mock provider only: "0000" simulates a decline. Ignored entirely when the Stripe
    /// provider is configured.</summary>
    public string? SimulatedLast4 { get; init; }
}

public record ConfirmSubscriptionRequest
{
    public string SubscriptionReference { get; init; } = string.Empty;
    public string OrderRef { get; init; } = string.Empty;
    public Guid UserId { get; init; }
    public decimal ExpectedAmount { get; init; }
    public string? SimulatedLast4 { get; init; }
}

public record CancelIntentRequest
{
    public string IntentId { get; init; } = string.Empty;
}

public record CancelSubscriptionRequest
{
    public string SubscriptionReference { get; init; } = string.Empty;

    /// <summary>True for a buyer-initiated cancellation -- they keep the period they already paid
    /// for. False only for compensation, where the subscription must end immediately.</summary>
    public bool AtPeriodEnd { get; init; } = true;

    /// <summary>Compensation only. Subscription invoices cannot be manual-capture, so an expired
    /// hold on that path means real money moved and has to come back.</summary>
    public bool RefundLastInvoice { get; init; }
}

public record PaymentResponse(
    Guid Id,
    decimal Amount,
    PaymentStatus Status,
    string OrderRef,
    string Currency,
    string? FailureCode,
    DateTime CreatedAt);

/// <param name="CurrentPeriodEnd">Inclusive last day of the period, matching Ticket.ValidTo. Taken
/// from the provider rather than computed locally, so our records and the buyer's card statement can
/// never disagree about what was paid for.</param>
public record SubscriptionChargeResponse(
    Guid Id,
    decimal Amount,
    PaymentStatus Status,
    string OrderRef,
    string Currency,
    string? FailureCode,
    string SubscriptionReference,
    DateOnly CurrentPeriodStart,
    DateOnly CurrentPeriodEnd);
