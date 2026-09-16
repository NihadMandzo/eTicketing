using eTicketing.Payment.Data.Entities;

namespace eTicketing.Payment.Business.Payments;

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
