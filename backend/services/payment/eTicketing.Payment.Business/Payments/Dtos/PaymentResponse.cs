using eTicketing.Payment.Data.Entities;

namespace eTicketing.Payment.Business.Payments;

public record PaymentResponse(
    Guid Id,
    decimal Amount,
    PaymentStatus Status,
    string OrderRef,
    string Currency,
    string? FailureCode,
    DateTime CreatedAt);
