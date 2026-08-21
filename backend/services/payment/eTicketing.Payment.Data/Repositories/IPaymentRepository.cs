using eTicketing.Contracts.Persistence;
using PaymentEntity = eTicketing.Payment.Data.Entities.Payment;

namespace eTicketing.Payment.Data.Repositories;

public interface IPaymentRepository : IRepository<PaymentEntity, Guid>
{
    /// <summary>Looks up a payment by its idempotency key (OrderRef is unique — see
    /// PaymentConfiguration). Used by PaymentService.ChargeAsync to detect a replayed charge
    /// request and return the original result instead of charging again.</summary>
    Task<PaymentEntity?> GetByOrderRefAsync(string orderRef, CancellationToken ct = default);
}
