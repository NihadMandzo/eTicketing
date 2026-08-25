using eTicketing.Contracts.Events;
using eTicketing.Contracts.Persistence;
using eTicketing.Ticketing.Business.Integration;
using eTicketing.Ticketing.Business.Tests.TestFixtures;
using eTicketing.Ticketing.Data.Entities;
using FluentAssertions;
using Moq;

namespace eTicketing.Ticketing.Business.Tests.Integration;

/// <summary>Covers the Catalog → Ticketing → Notifications hop that makes "every buyer hears about
/// a change" possible: Catalog knows what changed but not who bought, Ticketing knows who bought
/// but never sees the edit.</summary>
public class ProductChangeNotifierTests : IDisposable
{
    private readonly TicketingTestContext _fixture = new();
    private readonly IProductChangeNotifier _sut;

    private readonly Guid _productId = Guid.NewGuid();
    private readonly Guid _otherProductId = Guid.NewGuid();

    /// <summary>Matches TicketingTestContext.Clock's pinned start.</summary>
    private static readonly DateOnly Today = new(2026, 8, 24);

    public ProductChangeNotifierTests() => _sut = _fixture.CreateProductChangeNotifier();

    [Fact]
    public async Task NotifyBuyersAsync_PublishesOneNotificationPerBuyer_CarryingTheChangeList()
    {
        await SeedTicketAsync(email: "ana@example.com");
        await SeedTicketAsync(email: "marko@example.com");

        await _sut.NotifyBuyersAsync(Changed(new ProductFieldChange("Datum i vrijeme", "01.09.2026. 20:00", "02.09.2026. 20:00")));

        VerifyPublished("ana@example.com");
        VerifyPublished("marko@example.com");
        _fixture.EventPublisher.Verify(
            p => p.PublishAsync(EventNames.ProductChanged, It.IsAny<ProductChangedNotification>(), It.IsAny<CancellationToken>()),
            Times.Exactly(2));
    }

    [Fact]
    public async Task NotifyBuyersAsync_ForABuyerHoldingSeveralTickets_PublishesOnlyOnce()
    {
        var buyerId = Guid.NewGuid();
        await SeedTicketAsync(email: "ana@example.com", userId: buyerId);
        await SeedTicketAsync(email: "ana@example.com", userId: buyerId);
        await SeedTicketAsync(email: "ana@example.com", userId: buyerId);

        await _sut.NotifyBuyersAsync(Changed());

        _fixture.EventPublisher.Verify(
            p => p.PublishAsync(EventNames.ProductChanged, It.IsAny<ProductChangedNotification>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task NotifyBuyersAsync_ForwardsEveryChangedFieldUnmodified()
    {
        await SeedTicketAsync(email: "ana@example.com");
        var changes = new[]
        {
            new ProductFieldChange("Naziv", "Stari naziv", "Novi naziv"),
            new ProductFieldChange("Grad", "Sarajevo", "Mostar"),
        };

        await _sut.NotifyBuyersAsync(Changed(changes));

        _fixture.EventPublisher.Verify(
            p => p.PublishAsync(
                EventNames.ProductChanged,
                It.Is<ProductChangedNotification>(n => n.Changes.Count == 2 && n.Changes[1].NewValue == "Mostar"),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Theory]
    [InlineData(TicketStatus.Cancelled)]
    [InlineData(TicketStatus.Used)]
    [InlineData(TicketStatus.Processing)]
    public async Task NotifyBuyersAsync_SkipsBuyersWhoseTicketIsNoLongerLive(TicketStatus status)
    {
        // Nobody wants "the venue moved" about a show they already attended or a purchase that
        // never completed.
        await SeedTicketAsync(email: "ana@example.com", status: status);

        await _sut.NotifyBuyersAsync(Changed());

        VerifyNothingPublished();
    }

    [Fact]
    public async Task NotifyBuyersAsync_IncludesReadyTicketsAlongsideConfirmedOnes()
    {
        await SeedTicketAsync(email: "ana@example.com", status: TicketStatus.Ready);

        await _sut.NotifyBuyersAsync(Changed());

        VerifyPublished("ana@example.com");
    }

    [Fact]
    public async Task NotifyBuyersAsync_SkipsADayPassForAPastDate()
    {
        await SeedTicketAsync(email: "ana@example.com", mode: TicketingMode.DailyEntry, validDate: Today.AddDays(-1));

        await _sut.NotifyBuyersAsync(Changed());

        VerifyNothingPublished();
    }

    [Fact]
    public async Task NotifyBuyersAsync_IncludesADayPassForAFutureDate()
    {
        await SeedTicketAsync(email: "ana@example.com", mode: TicketingMode.DailyEntry, validDate: Today.AddDays(5));

        await _sut.NotifyBuyersAsync(Changed());

        VerifyPublished("ana@example.com");
    }

    [Fact]
    public async Task NotifyBuyersAsync_IgnoresBuyersOfADifferentProduct()
    {
        await SeedTicketAsync(email: "ana@example.com", productId: _otherProductId);

        await _sut.NotifyBuyersAsync(Changed());

        VerifyNothingPublished();
    }

    [Fact]
    public async Task NotifyBuyersAsync_WithNoBuyers_PublishesNothing()
    {
        await _sut.NotifyBuyersAsync(Changed());

        VerifyNothingPublished();
    }

    [Fact]
    public async Task NotifyBuyersAsync_WithAnEmptyChangeList_PublishesNothing()
    {
        // Guard against mailing people a change notice that lists no changes.
        await SeedTicketAsync(email: "ana@example.com");

        await _sut.NotifyBuyersAsync(new ProductUpdated(_productId, "Test proizvod", DateTime.UtcNow, []));

        VerifyNothingPublished();
    }

    [Fact]
    public async Task NotifyBuyersAsync_JustAfterLocalMidnight_StillIncludesADayPassForTheNewLocalDay()
    {
        // Europe/Sarajevo is UTC+2 in August, so 22:30 UTC is already 00:30 the next day. A day pass
        // for that new local day is still live and its holder still deserves the notice — deriving
        // "today" from UTC would filter them out as though the pass had expired. See PlatformClock.
        await SeedTicketAsync(email: "ana@example.com", mode: TicketingMode.DailyEntry, validDate: Today.AddDays(1));
        _fixture.Clock.SetUtcNow(new DateTimeOffset(2026, 8, 24, 22, 30, 0, TimeSpan.Zero));

        await _sut.NotifyBuyersAsync(Changed());

        VerifyPublished("ana@example.com");
    }

    [Fact]
    public async Task NotifyBuyersAsync_JustAfterLocalMidnight_SkipsADayPassForTheLocalDayThatJustEnded()
    {
        await SeedTicketAsync(email: "ana@example.com", mode: TicketingMode.DailyEntry, validDate: Today);
        _fixture.Clock.SetUtcNow(new DateTimeOffset(2026, 8, 24, 22, 30, 0, TimeSpan.Zero));

        await _sut.NotifyBuyersAsync(Changed());

        VerifyNothingPublished();
    }

    private ProductUpdated Changed(params ProductFieldChange[] changes) =>
        new(_productId, "Test proizvod", DateTime.UtcNow,
            changes.Length > 0 ? changes : [new ProductFieldChange("Naziv", "Staro", "Novo")]);

    private void VerifyPublished(string email) =>
        _fixture.EventPublisher.Verify(
            p => p.PublishAsync(
                EventNames.ProductChanged,
                It.Is<ProductChangedNotification>(n => n.RecipientEmail == email && n.ProductId == _productId),
                It.IsAny<CancellationToken>()),
            Times.Once);

    private void VerifyNothingPublished() =>
        _fixture.EventPublisher.Verify(
            p => p.PublishAsync(EventNames.ProductChanged, It.IsAny<ProductChangedNotification>(), It.IsAny<CancellationToken>()),
            Times.Never);

    private async Task SeedTicketAsync(
        string email,
        Guid? userId = null,
        Guid? productId = null,
        TicketStatus status = TicketStatus.Confirmed,
        TicketingMode mode = TicketingMode.SingleOccurrence,
        DateOnly? validDate = null)
    {
        var resolvedProductId = productId ?? _productId;
        var sector = new Sector
        {
            Id = Guid.NewGuid(),
            ProductId = resolvedProductId,
            OrganizationId = Guid.NewGuid(),
            Name = "VIP",
            Capacity = 100,
            Price = 50,
            Status = PublishStatus.Published,
            TicketingMode = mode,
        };
        _fixture.DbContext.Sectors.Add(sector);

        var ticket = mode == TicketingMode.DailyEntry
            ? Ticket.ForDailyEntry(sector.Id, null, Guid.NewGuid(), resolvedProductId, userId ?? Guid.NewGuid(), email, 50, validDate)
            : Ticket.ForSingleOccurrence(sector.Id, null, Guid.NewGuid(), resolvedProductId, userId ?? Guid.NewGuid(), email, 50);
        ticket.Status = status;

        _fixture.DbContext.Tickets.Add(ticket);
        await _fixture.DbContext.SaveChangesAsync();
        _fixture.DbContext.ChangeTracker.Clear();
    }

    public void Dispose() => _fixture.Dispose();
}
