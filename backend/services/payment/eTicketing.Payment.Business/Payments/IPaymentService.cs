using eTicketing.Contracts.Results;

namespace eTicketing.Payment.Business.Payments;

public interface IPaymentService
{
    /// <summary>Internal deterministic mock (see docs/payment-setup-guide.md) — a decline is a
    /// normal, expected business outcome represented in the response body (Status=Failed), not a
    /// Result failure; a Result failure here would mean something genuinely went wrong (e.g. a bug),
    /// not "the mock declined this card on purpose".</summary>
    Task<Result<PaymentResponse>> ChargeAsync(ChargeRequest request, CancellationToken ct = default);
}
