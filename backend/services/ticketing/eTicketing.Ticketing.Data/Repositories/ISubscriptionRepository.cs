using eTicketing.Contracts.Pagination;
using eTicketing.Contracts.Persistence;
using eTicketing.Ticketing.Data.Entities;

namespace eTicketing.Ticketing.Data.Repositories;

public interface ISubscriptionRepository : IRepository<Subscription, Guid>
{
    /// <summary>Resolves the subscription a provider webhook is about. Backed by the unique filtered
    /// index on PaymentReference -- without it every renewal, failure and cancellation would table
    /// scan.</summary>
    Task<Subscription?> GetByPaymentReferenceAsync(string paymentReference, CancellationToken ct = default);

    /// <summary>The caller's own subscriptions, newest first, with Sector loaded for display.</summary>
    Task<PagedResult<Subscription>> GetMineAsync(Guid userId, int? page, int? pageSize, CancellationToken ct = default);

    /// <summary>One subscription with its Sector, for ownership checks and cancellation.</summary>
    Task<Subscription?> GetByIdWithSectorAsync(Guid id, CancellationToken ct = default);
}
