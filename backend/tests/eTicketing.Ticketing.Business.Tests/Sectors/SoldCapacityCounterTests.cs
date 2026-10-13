using eTicketing.Contracts.Persistence;
using eTicketing.Ticketing.Business.Sectors;
using eTicketing.Ticketing.Business.Tests.TestFixtures;
using eTicketing.Ticketing.Data.Entities;
using FluentAssertions;

namespace eTicketing.Ticketing.Business.Tests.Sectors;

/// <summary>
/// What the database says is taken on a capacity counter — the number a lost Redis counter is
/// rebuilt from. Real repositories over Sqlite, because the whole point is which rows count.
/// </summary>
public class SoldCapacityCounterTests : IDisposable
{
    private readonly TicketingTestContext _fixture = new();
    private readonly ISoldCapacityCounter _sut;
    private readonly Guid _productId = Guid.NewGuid();

    public SoldCapacityCounterTests()
    {
        _sut = _fixture.SoldCapacityCounter;
    }

    private async Task<Sector> SeedSectorAsync(TicketingMode mode)
    {
        var sector = new Sector
        {
            Id = Guid.NewGuid(),
            ProductId = _productId,
            OrganizationId = Guid.NewGuid(),
            Name = "Parter",
            Capacity = 100,
            Price = 20,
            Status = PublishStatus.Published,
            TicketingMode = mode,
            PeriodYear = mode == TicketingMode.DailyEntry ? 2026 : null,
            PeriodMonth = mode == TicketingMode.DailyEntry ? 8 : null,
        };
        _fixture.DbContext.Sectors.Add(sector);
        await _fixture.DbContext.SaveChangesAsync();
        return sector;
    }

    private async Task SeedTicketsAsync(Sector sector, int count, TicketStatus status = TicketStatus.Confirmed, DateOnly? date = null)
    {
        for (var i = 0; i < count; i++)
        {
            var ticket = date is null
                ? Ticket.ForSingleOccurrence(sector.Id, null, Guid.NewGuid(), _productId, Guid.NewGuid(), "kupac@test.ba", 20)
                : Ticket.ForDailyEntry(sector.Id, null, Guid.NewGuid(), _productId, Guid.NewGuid(), "kupac@test.ba", 20, date);
            ticket.Status = status;
            _fixture.DbContext.Tickets.Add(ticket);
        }
        await _fixture.DbContext.SaveChangesAsync();
    }

    [Fact]
    public async Task CountAsync_ForSingleOccurrence_CountsEveryTicketThatIsNotCancelled()
    {
        var sector = await SeedSectorAsync(TicketingMode.SingleOccurrence);
        await SeedTicketsAsync(sector, 3, TicketStatus.Confirmed);
        await SeedTicketsAsync(sector, 2, TicketStatus.Used);
        await SeedTicketsAsync(sector, 1, TicketStatus.Ready);
        await SeedTicketsAsync(sector, 4, TicketStatus.Cancelled);

        (await _sut.CountAsync(sector, null)).Should().Be(6);
    }

    [Fact]
    public async Task CountAsync_ForSingleOccurrence_IgnoresOtherSectors()
    {
        var sector = await SeedSectorAsync(TicketingMode.SingleOccurrence);
        var other = await SeedSectorAsync(TicketingMode.SingleOccurrence);
        await SeedTicketsAsync(sector, 2);
        await SeedTicketsAsync(other, 5);

        (await _sut.CountAsync(sector, null)).Should().Be(2);
    }

    [Fact]
    public async Task CountAsync_ForDailyEntry_CountsOnlyTheRequestedDate()
    {
        var sector = await SeedSectorAsync(TicketingMode.DailyEntry);
        var day = new DateOnly(2026, 8, 15);
        await SeedTicketsAsync(sector, 3, date: day);
        await SeedTicketsAsync(sector, 7, date: day.AddDays(1));
        await SeedTicketsAsync(sector, 1, TicketStatus.Cancelled, day);

        (await _sut.CountAsync(sector, day)).Should().Be(3);
    }

    [Fact]
    public async Task CountAsync_ForRecurringReservation_CountsSubscriptionsNotRenewedTickets()
    {
        // One space per subscription, however many monthly tickets it has minted.
        var sector = await SeedSectorAsync(TicketingMode.RecurringReservation);
        var active = Subscription.Create(sector.Id, Guid.NewGuid(), "a@test.ba", new DateOnly(2026, 8, 1), new DateOnly(2026, 8, 31), "sub_1", "hold_1");
        var pastDue = Subscription.Create(sector.Id, Guid.NewGuid(), "b@test.ba", new DateOnly(2026, 8, 1), new DateOnly(2026, 8, 31), "sub_2", "hold_2");
        var cancelled = Subscription.Create(sector.Id, Guid.NewGuid(), "c@test.ba", new DateOnly(2026, 8, 1), new DateOnly(2026, 8, 31), "sub_3", "hold_3");
        pastDue.MarkPastDue();
        cancelled.Cancel(DateTime.UtcNow);
        _fixture.DbContext.Subscriptions.AddRange(active, pastDue, cancelled);
        for (var month = 1; month <= 3; month++)
        {
            _fixture.DbContext.Tickets.Add(Ticket.ForRecurringReservation(
                sector.Id, null, Guid.NewGuid(), _productId, Guid.NewGuid(), "a@test.ba", 80,
                active.Id, new DateOnly(2026, month, 1), new DateOnly(2026, month, 28)));
        }
        await _fixture.DbContext.SaveChangesAsync();

        (await _sut.CountAsync(sector, null)).Should().Be(2);
    }

    [Fact]
    public async Task CountAsync_ForASectorWithNothingSold_IsZero()
    {
        var sector = await SeedSectorAsync(TicketingMode.SingleOccurrence);

        (await _sut.CountAsync(sector, null)).Should().Be(0);
    }

    public void Dispose() => _fixture.Dispose();
}
