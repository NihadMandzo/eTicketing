namespace eTicketing.Ticketing.Business.External;

/// <summary>Mirrors eTicketing.Payment's SubscriptionChargeResponse. Carries the billing period the
/// provider settled on, which is what the Subscription row is stamped with -- so our records and the
/// buyer's card statement can never disagree about what was paid for.</summary>
public record SubscriptionChargeResponse(
    Guid Id,
    decimal Amount,
    PaymentChargeStatus Status,
    string OrderRef,
    string Currency,
    string? FailureCode,
    string SubscriptionReference,
    DateOnly CurrentPeriodStart,
    DateOnly CurrentPeriodEnd);
