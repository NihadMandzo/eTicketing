namespace eTicketing.Ticketing.Business.External;

/// <summary>Everything the client needs to drive the payment: which provider is configured (so it
/// renders the Stripe Elements form or the mock card form), the publishable key when there is one,
/// and the client secret to confirm against.</summary>
public record PaymentIntentCreatedResponse(
    string Provider,
    string? PublishableKey,
    string IntentId,
    string? ClientSecret,
    decimal Amount,
    string Currency,
    string? SubscriptionReference);
