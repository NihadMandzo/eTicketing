namespace eTicketing.Ticketing.Business.Purchases;

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
