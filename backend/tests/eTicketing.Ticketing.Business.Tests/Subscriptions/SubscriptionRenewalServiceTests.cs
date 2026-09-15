using eTicketing.Contracts.Events;
using eTicketing.Contracts.Persistence;
using eTicketing.Ticketing.Business.Sectors;
using eTicketing.Ticketing.Business.Subscriptions;
using eTicketing.Ticketing.Business.Tests.TestFixtures;
using eTicketing.Ticketing.Data.Entities;
using FluentAssertions;
using Moq;

namespace eTicketing.Ticketing.Business.Tests.Subscriptions;

/// <summary>
/// The webhook-driven half of subscriptions: what happens to a parking reservation months after
/// anyone was last on the site. Two behaviours here are the ones most likely to cost real money or
/// real inventory if they regress -- a redelivered renewal minting a duplicate ticket, and a
/// cancellation failing to hand the parking space back.
/// </summary>
public class SubscriptionRenewalServiceTests : IDisposable
{
    private readonly TicketingTestContext _fixture = new();
    private readonly ISubscriptionRenewalService _sut;

    private static readonly Guid Buyer = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private const string SubscriptionRef = "sub_1";
    private const string HoldId = "hold-parking-1";

    public SubscriptionRenewalServiceTests() => _sut = _fixture.CreateSubscriptionRenewalService();

    private async Task<(Sector Sector, Subscription Subscription)> SeedActiveSubscriptionAsync()
    {
        var sector = new Sector
        {
            Id = Guid.NewGuid(),
            ProductId = Guid.NewGuid(),
            OrganizationId = Guid.NewGuid(),
            Name = "A-12",
            Capacity = 1,
            Price = 60m,
            Status = PublishStatus.Published,
            TicketingMode = TicketingMode.RecurringReservation,
        };
        await _fixture.SectorRepository.AddAsync(sector);

        var subscription = new Subscription
        {
            Id = Guid.NewGuid(),
            SectorId = sector.Id,
            UserId = Buyer,
            UserEmail = "kupac@example.com",
            Status = SubscriptionStatus.Active,
            CurrentPeriodStart = new DateOnly(2026, 9, 1),
            CurrentPeriodEnd = new DateOnly(2026, 9, 30),
            PaymentReference = SubscriptionRef,
            CapacityHoldId = HoldId,
        };
        await _fixture.SubscriptionRepository.AddAsync(subscription);
        await _fixture.UnitOfWork.SaveChangesAsync();

        return (sector, subscription);
    }

    private static SubscriptionRenewed Renewal(DateOnly start, DateOnly end) =>
        new(SubscriptionRef, 60m, "eur", start, end, DateTime.UtcNow);

    [Fact]
    public async Task RenewAsync_AdvancesThePeriodAndMintsTheNextTicket()
    {
        var (sector, subscription) = await SeedActiveSubscriptionAsync();

        await _sut.RenewAsync(Renewal(new DateOnly(2026, 10, 1), new DateOnly(2026, 10, 31)));

        var stored = (await _fixture.SubscriptionRepository.GetByIdAsync(subscription.Id))!;
        stored.CurrentPeriodStart.Should().Be(new DateOnly(2026, 10, 1));
        stored.CurrentPeriodEnd.Should().Be(new DateOnly(2026, 10, 31));
        stored.NextRenewalAt.Should().Be(new DateTime(2026, 11, 1, 0, 0, 0, DateTimeKind.Unspecified));

        var ticket = _fixture.TicketRepository.Query().Single(t => t.SubscriptionId == subscription.Id);
        ticket.ValidFrom.Should().Be(new DateOnly(2026, 10, 1));
        ticket.ValidTo.Should().Be(new DateOnly(2026, 10, 31));
        ticket.SectorId.Should().Be(sector.Id);
        ticket.UserEmail.Should().Be("kupac@example.com");
    }

    /// <summary>Publishing the same event a synchronous purchase does is what makes the renewal
    /// email and PDF work with no extra wiring in Notifications or PdfGeneration.</summary>
    [Fact]
    public async Task RenewAsync_PublishesTicketPurchasedSoTheBuyerGetsTheNewTicket()
    {
        await SeedActiveSubscriptionAsync();

        await _sut.RenewAsync(Renewal(new DateOnly(2026, 10, 1), new DateOnly(2026, 10, 31)));

        _fixture.EventPublisher.Verify(
            p => p.PublishAsync(
                EventNames.TicketPurchased,
                It.Is<TicketPurchased>(e => e.UserId == Buyer && e.Tickets.Count == 1),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    /// <summary>
    /// The space was permanently decremented at first purchase and stays taken for the life of the
    /// subscription. Re-holding it would fail outright (remaining is zero), so a renewal must not go
    /// anywhere near the capacity lock.
    /// </summary>
    [Fact]
    public async Task RenewAsync_DoesNotTouchSectorCapacity()
    {
        await SeedActiveSubscriptionAsync();

        await _sut.RenewAsync(Renewal(new DateOnly(2026, 10, 1), new DateOnly(2026, 10, 31)));

        _fixture.CapacityLock.Verify(
            l => l.TryHoldAsync(It.IsAny<Guid>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<DateOnly?>(),
                It.IsAny<TimeSpan>(), It.IsAny<Guid?>(), It.IsAny<CancellationToken>()),
            Times.Never);
        _fixture.CapacityLock.Verify(
            l => l.ReleaseConfirmedAsync(It.IsAny<Guid>(), It.IsAny<DateOnly?>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    /// <summary>Provider webhooks are at-least-once. A redelivery must not sell the buyer a second
    /// month they never paid for.</summary>
    [Fact]
    public async Task RenewAsync_DeliveredTwiceForTheSamePeriod_MintsOnlyOneTicket()
    {
        var (_, subscription) = await SeedActiveSubscriptionAsync();
        var renewal = Renewal(new DateOnly(2026, 10, 1), new DateOnly(2026, 10, 31));

        await _sut.RenewAsync(renewal);
        await _sut.RenewAsync(renewal);

        _fixture.TicketRepository.Query().Count(t => t.SubscriptionId == subscription.Id).Should().Be(1);
    }

    /// <summary>A renewal that finally goes through clears PastDue -- the provider got paid in the end.</summary>
    [Fact]
    public async Task RenewAsync_ForAPastDueSubscription_ReturnsItToActive()
    {
        var (_, subscription) = await SeedActiveSubscriptionAsync();
        var tracked = (await _fixture.SubscriptionRepository.GetByIdAsync(subscription.Id))!;
        tracked.Status = SubscriptionStatus.PastDue;
        await _fixture.UnitOfWork.SaveChangesAsync();

        await _sut.RenewAsync(Renewal(new DateOnly(2026, 10, 1), new DateOnly(2026, 10, 31)));

        (await _fixture.SubscriptionRepository.GetByIdAsync(subscription.Id))!
            .Status.Should().Be(SubscriptionStatus.Active);
    }

    /// <summary>A stale reference must not dead-letter-storm the consumer forever.</summary>
    [Fact]
    public async Task RenewAsync_ForAnUnknownSubscription_IsIgnoredWithoutThrowing()
    {
        var act = async () => await _sut.RenewAsync(
            new SubscriptionRenewed("sub_unknown", 60m, "eur", new DateOnly(2026, 10, 1), new DateOnly(2026, 10, 31), DateTime.UtcNow));

        await act.Should().NotThrowAsync();
        _fixture.TicketRepository.Query().Should().BeEmpty();
    }

    [Fact]
    public async Task MarkPastDueAsync_SetsPastDueWithoutMintingOrReleasing()
    {
        var (_, subscription) = await SeedActiveSubscriptionAsync();

        await _sut.MarkPastDueAsync(new SubscriptionPaymentFailed(SubscriptionRef, "renewal_payment_failed", DateTime.UtcNow));

        (await _fixture.SubscriptionRepository.GetByIdAsync(subscription.Id))!
            .Status.Should().Be(SubscriptionStatus.PastDue);
        _fixture.TicketRepository.Query().Should().BeEmpty();

        // The provider is still working through its dunning retries and the buyer keeps the space
        // while it does. Only an actual cancellation frees it.
        _fixture.CapacityLock.Verify(
            l => l.ReleaseConfirmedAsync(It.IsAny<Guid>(), It.IsAny<DateOnly?>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    /// <summary>
    /// The single most important assertion in this file. The parking space was permanently
    /// decremented at first purchase, so if cancellation does not hand it back, that spot is sold
    /// forever and no one can ever reserve it again.
    /// </summary>
    [Fact]
    public async Task CancelAsync_ReleasesTheParkingSpaceBackToTheSector()
    {
        var (sector, subscription) = await SeedActiveSubscriptionAsync();

        await _sut.CancelAsync(new SubscriptionCancelled(SubscriptionRef, DateTime.UtcNow));

        var stored = (await _fixture.SubscriptionRepository.GetByIdAsync(subscription.Id))!;
        stored.Status.Should().Be(SubscriptionStatus.Cancelled);
        stored.CancelledAt.Should().NotBeNull();
        stored.NextRenewalAt.Should().BeNull();

        // ReleaseConfirmedAsync, not ReleaseAsync: the hold was confirmed months ago and its
        // holdinfo pointer is long gone, which is exactly why CapacityHoldId is stored.
        _fixture.CapacityLock.Verify(
            l => l.ReleaseConfirmedAsync(sector.Id, null, HoldId, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task CancelAsync_DeliveredTwice_ReleasesTheSpaceOnlyOnce()
    {
        await SeedActiveSubscriptionAsync();
        var cancelled = new SubscriptionCancelled(SubscriptionRef, DateTime.UtcNow);

        await _sut.CancelAsync(cancelled);
        await _sut.CancelAsync(cancelled);

        _fixture.CapacityLock.Verify(
            l => l.ReleaseConfirmedAsync(It.IsAny<Guid>(), It.IsAny<DateOnly?>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    /// <summary>A late failure notice must not resurrect a subscription that already ended.</summary>
    [Fact]
    public async Task MarkPastDueAsync_ForAnAlreadyCancelledSubscription_ChangesNothing()
    {
        var (_, subscription) = await SeedActiveSubscriptionAsync();
        await _sut.CancelAsync(new SubscriptionCancelled(SubscriptionRef, DateTime.UtcNow));

        await _sut.MarkPastDueAsync(new SubscriptionPaymentFailed(SubscriptionRef, "renewal_payment_failed", DateTime.UtcNow));

        (await _fixture.SubscriptionRepository.GetByIdAsync(subscription.Id))!
            .Status.Should().Be(SubscriptionStatus.Cancelled);
    }

    public void Dispose() => _fixture.Dispose();
}
