using eTicketing.Contracts.Persistence;
using eTicketing.Payment.Data.Entities;

namespace eTicketing.Payment.Data.Repositories;

public interface IStripeCustomerRepository : IRepository<StripeCustomer, Guid>
{
    Task<StripeCustomer?> GetByUserIdAsync(Guid userId, CancellationToken ct = default);
}
