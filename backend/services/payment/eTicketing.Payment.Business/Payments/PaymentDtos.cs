using eTicketing.Payment.Data.Entities;

namespace eTicketing.Payment.Business.Payments;

public record ChargeRequest
{
    public decimal Amount { get; init; }
    public string OrderRef { get; init; } = string.Empty;

    // Only the last 4 digits ever reach this service — see PurchaseRequest's doc comment
    // (eTicketing.Ticketing.Business.Purchases.PurchaseDtos) for why nothing card-shaped is
    // ever forwarded past PurchaseService.Last4 on the Ticketing side.
    public string CardNumberLast4 { get; init; } = string.Empty;
}

public record PaymentResponse(Guid Id, decimal Amount, PaymentStatus Status, string OrderRef, DateTime CreatedAt);
