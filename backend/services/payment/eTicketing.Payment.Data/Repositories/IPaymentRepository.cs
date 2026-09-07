using eTicketing.Contracts.Persistence;

using PaymentEntity = eTicketing.Payment.Data.Entities.Payment;

namespace eTicketing.Payment.Data.Repositories;

public interface IPaymentRepository : IRepository<PaymentEntity, Guid>
{
    /// <summary>Looks up a payment by its idempotency key (OrderRef is unique -- see
    /// PaymentConfiguration). Used to detect a replayed request and return the original result
    /// instead of going to the provider again.</summary>
    Task<PaymentEntity?> GetByOrderRefAsync(string orderRef, CancellationToken ct = default);

    /// <summary>Resolves the row a capture refers to, so the stored OrderRef and buyer -- not the
    /// client's claims -- decide what is being captured.</summary>
    Task<PaymentEntity?> GetByProviderIntentIdAsync(string providerIntentId, CancellationToken ct = default);

    /// <summary>Most recent payment for a subscription. A renewal webhook uses this to find the
    /// order the subscription belongs to.</summary>
    Task<PaymentEntity?> GetLatestBySubscriptionIdAsync(string providerSubscriptionId, CancellationToken ct = default);
}
