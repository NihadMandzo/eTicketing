namespace eTicketing.Payment.Business.Payments;

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
