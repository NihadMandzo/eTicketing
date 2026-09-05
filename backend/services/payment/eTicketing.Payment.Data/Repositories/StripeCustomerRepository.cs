using eTicketing.Contracts.Persistence;
using eTicketing.Payment.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace eTicketing.Payment.Data.Repositories;

public class StripeCustomerRepository : Repository<StripeCustomer, Guid>, IStripeCustomerRepository
{
    public StripeCustomerRepository(PaymentDbContext context) : base(context) { }

    public Task<StripeCustomer?> GetByUserIdAsync(Guid userId, CancellationToken ct = default)
        => DbSet.FirstOrDefaultAsync(c => c.UserId == userId, ct);
}
