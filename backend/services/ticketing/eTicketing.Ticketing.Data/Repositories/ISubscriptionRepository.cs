using eTicketing.Contracts.Persistence;
using eTicketing.Ticketing.Data.Entities;

namespace eTicketing.Ticketing.Data.Repositories;

public interface ISubscriptionRepository : IRepository<Subscription, Guid>
{
}
