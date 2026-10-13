using eTicketing.Contracts.Persistence;
using eTicketing.Ticketing.Data.Entities;
using eTicketing.Ticketing.Data.Repositories;

namespace eTicketing.Ticketing.Business.Sectors;

public class SoldCapacityCounter : ISoldCapacityCounter
{
    private readonly ITicketRepository _ticketRepository;
    private readonly ISubscriptionRepository _subscriptionRepository;

    public SoldCapacityCounter(ITicketRepository ticketRepository, ISubscriptionRepository subscriptionRepository)
    {
        _ticketRepository = ticketRepository;
        _subscriptionRepository = subscriptionRepository;
    }

    public Task<int> CountAsync(Sector sector, DateOnly? date, CancellationToken ct = default) =>
        sector.TicketingMode switch
        {
            // One space per subscription, held from the first purchase until cancellation. Counting
            // tickets instead would count every renewed month as another space.
            TicketingMode.RecurringReservation => _subscriptionRepository.CountHoldingSpaceAsync(sector.Id, ct),
            TicketingMode.DailyEntry => _ticketRepository.CountOccupyingSeatsAsync(sector.Id, date, ct),
            _ => _ticketRepository.CountOccupyingSeatsAsync(sector.Id, null, ct),
        };
}
