using System.Security.Claims;
using eTicketing.Contracts.Persistence;
using eTicketing.Contracts.Results;
using eTicketing.Ticketing.Business.External;
using eTicketing.Ticketing.Business.Subscriptions;
using eTicketing.Ticketing.Business.Tests.TestFixtures;
using eTicketing.Ticketing.Data.Entities;
using FluentAssertions;
using Moq;

namespace eTicketing.Ticketing.Business.Tests.Subscriptions;

/// <summary>
/// Buyer-facing subscription management. The behaviour worth protecting here is that cancelling is
/// NOT immediate: the buyer paid for the current month and keeps it, so Status stays Active and the
/// parking space stays theirs until the provider confirms the subscription has actually ended.
/// </summary>
public class SubscriptionServiceTests : IDisposable
{
    private readonly TicketingTestContext _fixture = new();
    private readonly ISubscriptionService _sut;

    private static readonly Guid Owner = Guid.Parse("33333333-3333-3333-3333-333333333333");
    private static readonly Guid Stranger = Guid.Parse("44444444-4444-4444-4444-444444444444");

    public SubscriptionServiceTests() => _sut = _fixture.CreateSubscriptionService();

    private static ClaimsPrincipal Caller(Guid userId, string role = "User")
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, userId.ToString()),
            new(ClaimTypes.Email, "kupac@example.com"),
            new(ClaimTypes.Role, role),
        };
        return new ClaimsPrincipal(new ClaimsIdentity(claims, "TestAuth"));
    }

    private async Task<Subscription> SeedAsync(Guid userId, SubscriptionStatus status = SubscriptionStatus.Active)
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
            UserId = userId,
            UserEmail = "kupac@example.com",
            Status = status,
            CurrentPeriodStart = new DateOnly(2026, 9, 1),
            CurrentPeriodEnd = new DateOnly(2026, 9, 30),
            PaymentReference = $"sub_{Guid.NewGuid():N}",
            CapacityHoldId = "hold-1",
        };
        await _fixture.SubscriptionRepository.AddAsync(subscription);
        await _fixture.UnitOfWork.SaveChangesAsync();

        return subscription;
    }

    private void PaymentAcceptsCancellation() =>
        _fixture.PaymentClient
            .Setup(p => p.CancelSubscriptionAsync(It.IsAny<string>(), true, false, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

    [Fact]
    public async Task GetMineAsync_ReturnsOnlyTheCallersOwnSubscriptions()
    {
        await SeedAsync(Owner);
        await SeedAsync(Owner);
        await SeedAsync(Stranger);

        var result = await _sut.GetMineAsync(new SubscriptionQuery(), Caller(Owner));

        result.IsSuccess.Should().BeTrue();
        result.Value!.TotalCount.Should().Be(2);
        result.Value.Items.Should().HaveCount(2);
        result.Value.Items.Should().OnlyContain(s => s.SectorName == "A-12");
    }

    [Fact]
    public async Task CancelAsync_ByTheOwner_SchedulesCancellationButKeepsTheSubscriptionActive()
    {
        var subscription = await SeedAsync(Owner);
        PaymentAcceptsCancellation();

        var result = await _sut.CancelAsync(subscription.Id, Caller(Owner));

        result.IsSuccess.Should().BeTrue();

        var stored = (await _fixture.SubscriptionRepository.GetByIdAsync(subscription.Id))!;
        stored.CancelAtPeriodEnd.Should().BeTrue();
        // Still Active on purpose: they paid for this month and their ticket is valid until it ends.
        stored.Status.Should().Be(SubscriptionStatus.Active);
        stored.CancelledAt.Should().BeNull();

        // atPeriodEnd: true, refundLastInvoice: false -- a voluntary cancellation refunds nothing.
        _fixture.PaymentClient.Verify(
            p => p.CancelSubscriptionAsync(subscription.PaymentReference!, true, false, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task CancelAsync_ByAnotherUser_IsForbiddenAndCancelsNothing()
    {
        var subscription = await SeedAsync(Owner);

        var result = await _sut.CancelAsync(subscription.Id, Caller(Stranger));

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("subscription.forbidden");
        result.Error.Type.Should().Be(ErrorType.Unauthorized);
        _fixture.PaymentClient.Verify(
            p => p.CancelSubscriptionAsync(It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    /// <summary>Platform staff bypass ownership everywhere else in this service; subscriptions are
    /// no exception.</summary>
    [Fact]
    public async Task CancelAsync_ByPlatformStaff_IsAllowedForAnyBuyer()
    {
        var subscription = await SeedAsync(Owner);
        PaymentAcceptsCancellation();

        var result = await _sut.CancelAsync(subscription.Id, Caller(Stranger, "SuperAdmin"));

        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task CancelAsync_ForAnUnknownSubscription_ReturnsNotFound()
    {
        var result = await _sut.CancelAsync(Guid.NewGuid(), Caller(Owner));

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("subscription.not_found");
    }

    [Fact]
    public async Task CancelAsync_ForAnAlreadyCancelledSubscription_ReturnsConflict()
    {
        var subscription = await SeedAsync(Owner, SubscriptionStatus.Cancelled);

        var result = await _sut.CancelAsync(subscription.Id, Caller(Owner));

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("subscription.already_cancelled");
        result.Error.Type.Should().Be(ErrorType.Conflict);
    }

    [Fact]
    public async Task CancelAsync_CalledTwice_ReturnsConflictOnTheSecondAttempt()
    {
        var subscription = await SeedAsync(Owner);
        PaymentAcceptsCancellation();

        await _sut.CancelAsync(subscription.Id, Caller(Owner));
        var second = await _sut.CancelAsync(subscription.Id, Caller(Owner));

        second.IsFailure.Should().BeTrue();
        second.Error.Code.Should().Be("subscription.already_cancelled");
    }

    /// <summary>A payment outage must not leave the row saying "cancelled" when the provider never
    /// heard about it -- the subscription would keep billing.</summary>
    [Fact]
    public async Task CancelAsync_WhenPaymentIsUnavailable_Returns503AndLeavesTheRowUnchanged()
    {
        var subscription = await SeedAsync(Owner);
        _fixture.PaymentClient
            .Setup(p => p.CancelSubscriptionAsync(It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new PaymentUnavailableException("down"));

        var result = await _sut.CancelAsync(subscription.Id, Caller(Owner));

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("payment.unavailable");
        result.Error.Type.Should().Be(ErrorType.Failure);
        (await _fixture.SubscriptionRepository.GetByIdAsync(subscription.Id))!
            .CancelAtPeriodEnd.Should().BeFalse();
    }

    public void Dispose() => _fixture.Dispose();
}
