namespace eTicketing.Payment.Business.Payments.Gateways;

/// <param name="CustomerReference">Already-resolved provider customer ("cus_..."), looked up or
/// created by PaymentService and persisted on StripeCustomer. Passing it in rather than resolving it
/// here is what stops a buyer who subscribes twice from ending up with two provider customers and
/// two saved cards.</param>
public record GatewaySubscriptionRequest(
    decimal AmountPerPeriod,
    string Currency,
    string OrderRef,
    Guid UserId,
    string CustomerReference,
    string ProductName,
    IReadOnlyDictionary<string, string> Metadata);
