using eTicketing.Contracts.Persistence;
using Microsoft.EntityFrameworkCore;

using PaymentEntity = eTicketing.Payment.Data.Entities.Payment;

namespace eTicketing.Payment.Data.Repositories;

public class PaymentRepository : Repository<PaymentEntity, Guid>, IPaymentRepository
{
    public PaymentRepository(PaymentDbContext context) : base(context) { }

    public Task<PaymentEntity?> GetByOrderRefAsync(string orderRef, CancellationToken ct = default)
        => DbSet.FirstOrDefaultAsync(p => p.OrderRef == orderRef, ct);

    public Task<PaymentEntity?> GetByProviderIntentIdAsync(string providerIntentId, CancellationToken ct = default)
        => DbSet.FirstOrDefaultAsync(p => p.ProviderPaymentIntentId == providerIntentId, ct);

    public Task<PaymentEntity?> GetLatestBySubscriptionIdAsync(string providerSubscriptionId, CancellationToken ct = default)
        => DbSet
            .Where(p => p.ProviderSubscriptionId == providerSubscriptionId)
            .OrderByDescending(p => p.CreatedAt)
            .FirstOrDefaultAsync(ct);
}
