using eTicketing.Contracts.Persistence;
using eTicketing.Ticketing.Data.Entities;

namespace eTicketing.Ticketing.Data.Repositories;

public class SubscriptionRepository : Repository<Subscription, Guid>, ISubscriptionRepository
{
    public SubscriptionRepository(TicketingDbContext context) : base(context) { }
}
