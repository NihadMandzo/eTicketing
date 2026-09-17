namespace eTicketing.Payment.Business.Payments.Gateways;

/// <param name="ClientSecret">Of the first invoice's PaymentIntent. The subscription is created
/// with payment_behavior=default_incomplete precisely so period one is a real invoice the client
/// confirms with the same Elements instance a one-time purchase uses, which keeps period one
/// structurally identical to every renewal.</param>
public record GatewaySubscriptionResult(
    string SubscriptionId,
    string? ClientSecret,
    string? InvoicePaymentIntentId,
    GatewayPaymentStatus Status,
    string? FailureCode,
    DateOnly CurrentPeriodStart,
    DateOnly CurrentPeriodEnd);
