using eTicketing.Contracts.Persistence;

namespace eTicketing.Payment.Data.Entities;

public enum PaymentStatus
{
    Succeeded,
    Failed,
}

/// <summary>
/// One row per charge attempt made by eTicketing.Ticketing's PurchaseService. This is an internal
/// mock (see docs/payment-setup-guide.md) — Status is decided deterministically by
/// PaymentService.ChargeAsync (a CardNumberLast4 of "0000" simulates a decline), never real card
/// processing. No card data is ever stored here, not even the last-4 digits.
/// </summary>
public class Payment : BaseEntity
{
    public Guid Id { get; set; }
    public decimal Amount { get; set; }
    public PaymentStatus Status { get; set; }

    // Matches the OrderId eTicketing.Ticketing's PurchaseService mints for the corresponding order.
    public string OrderRef { get; set; } = string.Empty;
}
