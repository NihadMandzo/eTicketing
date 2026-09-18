namespace eTicketing.Payment.Business.Payments.Gateways;

/// <param name="Amount">In major units (the KM figure the UI shows). Converted to the provider's
/// integer minor units exactly once, by MoneyConverter.</param>
/// <param name="Metadata">Written onto the provider object and read back at capture time to prove
/// the intent belongs to this order and this buyer. The client is never believed on its own.</param>
public record GatewayIntentRequest(
    decimal Amount,
    string Currency,
    string OrderRef,
    Guid UserId,
    string? CustomerEmail,
    string Description,
    IReadOnlyDictionary<string, string> Metadata);
