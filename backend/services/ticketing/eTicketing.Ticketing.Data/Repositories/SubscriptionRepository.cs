using eTicketing.Contracts.Pagination;
using eTicketing.Contracts.Persistence;
using eTicketing.Ticketing.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace eTicketing.Ticketing.Data.Repositories;

public class SubscriptionRepository : Repository<Subscription, Guid>, ISubscriptionRepository
{
    public SubscriptionRepository(TicketingDbContext context) : base(context) { }

    public Task<Subscription?> GetByPaymentReferenceAsync(string paymentReference, CancellationToken ct = default)
        => DbSet
            .Include(s => s.Sector)
            .FirstOrDefaultAsync(s => s.PaymentReference == paymentReference, ct);

    // AsNoTracking: the only caller maps these straight to a response. GetByIdWithSectorAsync
    // below stays tracked — cancellation writes through the instance it returns.
    public Task<PagedResult<Subscription>> GetMineAsync(Guid userId, int? page, int? pageSize, CancellationToken ct = default)
        => DbSet
            .AsNoTracking()
            .Include(s => s.Sector)
            .Where(s => s.UserId == userId)
            .OrderByDescending(s => s.CreatedAt)
            .ToPagedResultAsync(
                page ?? BaseSearchObject.DefaultPage,
                pageSize ?? BaseSearchObject.DefaultPageSize,
                ct);

    public Task<Subscription?> GetByIdWithSectorAsync(Guid id, CancellationToken ct = default)
        => DbSet
            .Include(s => s.Sector)
            .FirstOrDefaultAsync(s => s.Id == id, ct);
}
