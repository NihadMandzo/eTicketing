using eTicketing.Payment.Data.Entities;

namespace eTicketing.Payment.Data.Repositories;

public interface IStripeEventRepository
{
    Task AddAsync(StripeEvent stripeEvent, CancellationToken ct = default);

    Task<bool> ExistsAsync(string stripeEventId, CancellationToken ct = default);
}
