using eTicketing.Payment.Data.Entities;

namespace eTicketing.Payment.Business.Payments;

public record ChargeRequest
{
    public decimal Amount { get; init; }
    public string OrderRef { get; init; } = string.Empty;

    // Only the last 4 digits ever reach this service — see the ChargeRequestValidator/PurchaseService
    // doc comments on the Ticketing side for why nothing card-shaped is ever forwarded further.
    public string CardNumberLast4 { get; init; } = string.Empty;
}

public record PaymentResponse(Guid Id, decimal Amount, PaymentStatus Status, string OrderRef, DateTime CreatedAt);
