using eTicketing.Contracts.Persistence;
using eTicketing.Ticketing.Business.Sectors;
using eTicketing.Ticketing.Data.Entities;
using FluentAssertions;
using Moq;

namespace eTicketing.Ticketing.Business.Tests.Sectors;

/// <summary>
/// Try the hold; if Redis has no counter at all, rebuild it from the database and try once more —
/// so a counter Redis lost cannot start again from zero and resell what was already sold.
/// </summary>
public class CapacityHoldRecoveryTests
{
    private readonly Mock<ISectorCapacityLock> _lock = new();
    private readonly Mock<ISoldCapacityCounter> _sold = new();
    private static readonly TimeSpan Ttl = TimeSpan.FromMinutes(5);
    private static readonly Guid Owner = Guid.NewGuid();

    private static Sector NewSector(int capacity = 500) => new()
    {
        Id = Guid.NewGuid(),
        ProductId = Guid.NewGuid(),
        OrganizationId = Guid.NewGuid(),
        Name = "Flash",
        Capacity = capacity,
        Price = 50,
        Status = PublishStatus.Published,
        TicketingMode = TicketingMode.SingleOccurrence,
    };

    private void GivenHoldResults(Sector sector, DateOnly? date, params HoldResult[] results)
    {
        var sequence = _lock.SetupSequence(l => l.TryHoldAsync(
            sector.Id, sector.Capacity, 1, date, Ttl, Owner, It.IsAny<CancellationToken>()));
        foreach (var result in results)
            sequence = sequence.ReturnsAsync(result);
    }

    private Task<HoldResult> HoldAsync(Sector sector, DateOnly? date = null) =>
        _lock.Object.TryHoldRecoveringAsync(_sold.Object, sector, 1, date, Ttl, Owner);

    [Fact]
    public async Task TryHoldRecovering_WhenTheCounterExists_DoesNotTouchTheDatabase()
    {
        var sector = NewSector();
        GivenHoldResults(sector, null, new HoldResult(true, "h1", DateTime.UtcNow.AddMinutes(5)));

        var result = await HoldAsync(sector);

        result.Success.Should().BeTrue();
        _sold.Verify(s => s.CountAsync(It.IsAny<Sector>(), It.IsAny<DateOnly?>(), It.IsAny<CancellationToken>()), Times.Never);
        _lock.Verify(l => l.SeedAsync(It.IsAny<Guid>(), It.IsAny<DateOnly?>(), It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task TryHoldRecovering_WhenTheCounterIsMissing_SeedsItFromTheDatabaseAndRetries()
    {
        var sector = NewSector();
        _sold.Setup(s => s.CountAsync(sector, null, It.IsAny<CancellationToken>())).ReturnsAsync(437);
        GivenHoldResults(sector, null,
            new HoldResult(false, null, null, CounterMissing: true),
            new HoldResult(true, "h1", DateTime.UtcNow.AddMinutes(5)));

        var result = await HoldAsync(sector);

        result.Success.Should().BeTrue();
        result.HoldId.Should().Be("h1");
        _lock.Verify(l => l.SeedAsync(sector.Id, null, 437, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task TryHoldRecovering_WhenTheRebuiltCounterIsFull_ReturnsNoCapacity()
    {
        // The case the rebuild exists for: Redis lost a sold-out sector. Without the rebuild the
        // retry would see an empty counter and sell the seat again.
        var sector = NewSector(capacity: 500);
        _sold.Setup(s => s.CountAsync(sector, null, It.IsAny<CancellationToken>())).ReturnsAsync(500);
        GivenHoldResults(sector, null,
            new HoldResult(false, null, null, CounterMissing: true),
            new HoldResult(false, null, null));

        var result = await HoldAsync(sector);

        result.Success.Should().BeFalse();
        result.CounterMissing.Should().BeFalse();
        _lock.Verify(l => l.SeedAsync(sector.Id, null, 500, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task TryHoldRecovering_ForADailyEntryCounter_SeedsThatDateOnly()
    {
        var sector = NewSector();
        var day = new DateOnly(2026, 8, 15);
        _sold.Setup(s => s.CountAsync(sector, day, It.IsAny<CancellationToken>())).ReturnsAsync(12);
        GivenHoldResults(sector, day,
            new HoldResult(false, null, null, CounterMissing: true),
            new HoldResult(true, "h1", DateTime.UtcNow.AddMinutes(5)));

        await HoldAsync(sector, day);

        _lock.Verify(l => l.SeedAsync(sector.Id, day, 12, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task TryHoldRecovering_RebuildsAtMostOncePerCall()
    {
        var sector = NewSector();
        GivenHoldResults(sector, null,
            new HoldResult(false, null, null, CounterMissing: true),
            new HoldResult(false, null, null, CounterMissing: true));

        var result = await HoldAsync(sector);

        result.Success.Should().BeFalse();
        _lock.Verify(l => l.SeedAsync(It.IsAny<Guid>(), It.IsAny<DateOnly?>(), It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Once);
        _lock.Verify(l => l.TryHoldAsync(It.IsAny<Guid>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<DateOnly?>(), It.IsAny<TimeSpan>(), It.IsAny<Guid?>(), It.IsAny<CancellationToken>()), Times.Exactly(2));
    }
}
