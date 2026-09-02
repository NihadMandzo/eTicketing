using eTicketing.Contracts.Events;
using eTicketing.Contracts.Persistence;
using eTicketing.Ticketing.Business.External;
using eTicketing.Ticketing.Business.Integration;
using eTicketing.Ticketing.Business.Tests.TestFixtures;
using eTicketing.Ticketing.Data.Entities;
using FluentAssertions;
using Moq;

namespace eTicketing.Ticketing.Business.Tests.Integration;

/// <summary>
/// Covers the Catalog → Ticketing → Notifications hop for a deleted product: every buyer holding a
/// still-valid ticket is told their ticket is void and who to ask for a refund, and the owning
/// organization is told separately when platform staff — not the organizer — did the deleting.
/// </summary>
public class ProductDeletionNotifierTests : IDisposable
{
    private readonly TicketingTestContext _fixture = new();
    private readonly IProductDeletionNotifier _sut;

    private readonly Guid _productId = Guid.NewGuid();
    private readonly Guid _otherProductId = Guid.NewGuid();
    private readonly Guid _organizationId = Guid.NewGuid();

    /// <summary>Matches TicketingTestContext.Clock's pinned start.</summary>
    private static readonly DateOnly Today = new(2026, 8, 24);

    public ProductDeletionNotifierTests()
    {
        _sut = _fixture.CreateProductDeletionNotifier();
        GivenContact();
    }

    // ── buyers ───────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task NotifyAsync_PublishesOneNotificationPerBuyer()
    {
        await SeedTicketAsync("ana@example.com");
        await SeedTicketAsync("marko@example.com");

        await _sut.NotifyAsync(Deleted());

        VerifyPublishedTo("ana@example.com", ProductDeletedAudience.Buyer);
        VerifyPublishedTo("marko@example.com", ProductDeletedAudience.Buyer);
        VerifyTotalPublished(2);
    }

    [Fact]
    public async Task NotifyAsync_ForABuyerHoldingSeveralTickets_PublishesOnceCarryingTheCount()
    {
        var buyerId = Guid.NewGuid();
        await SeedTicketAsync("ana@example.com", userId: buyerId);
        await SeedTicketAsync("ana@example.com", userId: buyerId);
        await SeedTicketAsync("ana@example.com", userId: buyerId);

        await _sut.NotifyAsync(Deleted());

        VerifyTotalPublished(1);
        VerifyPublished(n => n.RecipientEmail == "ana@example.com" && n.TicketCount == 3);
    }

    [Fact]
    public async Task NotifyAsync_CarriesTheOrganizersContactSoTheBuyerKnowsWhoToAskForARefund()
    {
        await SeedTicketAsync("ana@example.com");

        await _sut.NotifyAsync(Deleted());

        VerifyPublished(n =>
            n.OrganizerName == "Sarajevo Events"
            && n.OrganizerEmail == "kontakt@sarajevo-events.ba"
            && n.OrganizerPhone == "+387 33 123 456");
    }

    [Fact]
    public async Task NotifyAsync_CarriesTheProductDateSoTheEmailCanNameWhatWasCancelled()
    {
        await SeedTicketAsync("ana@example.com");
        var date = new DateTime(2026, 10, 12, 20, 0, 0, DateTimeKind.Utc);

        await _sut.NotifyAsync(Deleted() with { ProductDate = date });

        VerifyPublished(n => n.ProductDate == date);
    }

    [Fact]
    public async Task NotifyAsync_IgnoresBuyersOfADifferentProduct()
    {
        await SeedTicketAsync("ana@example.com");
        await SeedTicketAsync("tudja@example.com", productId: _otherProductId);

        await _sut.NotifyAsync(Deleted());

        VerifyPublishedTo("ana@example.com", ProductDeletedAudience.Buyer);
        VerifyTotalPublished(1);
    }

    [Fact]
    public async Task NotifyAsync_SkipsBuyersWhoseTicketIsNoLongerLive()
    {
        await SeedTicketAsync("otkazana@example.com", status: TicketStatus.Cancelled);
        await SeedTicketAsync("iskoristena@example.com", status: TicketStatus.Used);
        await SeedTicketAsync("vazeca@example.com");

        await _sut.NotifyAsync(Deleted());

        VerifyPublishedTo("vazeca@example.com", ProductDeletedAudience.Buyer);
        VerifyTotalPublished(1);
    }

    [Fact]
    public async Task NotifyAsync_SkipsADayPassForADateThatHasPassed()
    {
        // Nothing to refund on a day pass whose day is behind us — the event being removed from
        // the catalogue cannot affect a visit that already happened.
        await SeedTicketAsync("prosla@example.com",
            mode: TicketingMode.DailyEntry, validDate: Today.AddDays(-1));
        await SeedTicketAsync("buduca@example.com",
            mode: TicketingMode.DailyEntry, validDate: Today.AddDays(3));

        await _sut.NotifyAsync(Deleted());

        VerifyPublishedTo("buduca@example.com", ProductDeletedAudience.Buyer);
        VerifyTotalPublished(1);
    }

    [Fact]
    public async Task NotifyAsync_WithNoBuyers_PublishesNothing()
    {
        await _sut.NotifyAsync(Deleted());

        VerifyTotalPublished(0);
    }

    // ── the organization ─────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task NotifyAsync_WhenPlatformStaffDeleted_AlsoNotifiesTheOrganizationSuperAdmin()
    {
        await SeedTicketAsync("ana@example.com");

        await _sut.NotifyAsync(Deleted(byPlatformStaff: true));

        VerifyPublishedTo("ana@example.com", ProductDeletedAudience.Buyer);
        VerifyPublishedTo("emir@sarajevo-events.ba", ProductDeletedAudience.Organizer);
        VerifyTotalPublished(2);
    }

    [Fact]
    public async Task NotifyAsync_TellsTheOrganizationHowManyBuyersItNowOwesRefunds()
    {
        await SeedTicketAsync("ana@example.com");
        await SeedTicketAsync("marko@example.com");

        await _sut.NotifyAsync(Deleted(byPlatformStaff: true));

        VerifyPublished(n => n.Audience == ProductDeletedAudience.Organizer && n.TicketCount == 2);
    }

    [Fact]
    public async Task NotifyAsync_WhenTheOrganizerDeletedTheirOwnProduct_NotifiesOnlyTheBuyers()
    {
        // They just did it themselves — an email telling them so is noise.
        await SeedTicketAsync("ana@example.com");

        await _sut.NotifyAsync(Deleted(byPlatformStaff: false));

        VerifyTotalPublished(1);
        _fixture.EventPublisher.Verify(
            p => p.PublishAsync(
                EventNames.ProductDeletedNotification,
                It.Is<ProductDeletedNotification>(n => n.Audience == ProductDeletedAudience.Organizer),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task NotifyAsync_ForADraftWithNoBuyers_StillNotifiesTheOrganization()
    {
        // The whole reason Catalog publishes for drafts as well: nobody bought anything, but the
        // organization still has a product missing that it did not remove.
        await _sut.NotifyAsync(Deleted(byPlatformStaff: true));

        VerifyPublishedTo("emir@sarajevo-events.ba", ProductDeletedAudience.Organizer);
        VerifyPublished(n => n.Audience == ProductDeletedAudience.Organizer && n.TicketCount == 0);
        VerifyTotalPublished(1);
    }

    [Fact]
    public async Task NotifyAsync_WithNoSuperAdminOnFile_FallsBackToTheOrganizationsOwnAddress()
    {
        GivenContact(superAdminEmail: null);

        await _sut.NotifyAsync(Deleted(byPlatformStaff: true));

        VerifyPublishedTo("kontakt@sarajevo-events.ba", ProductDeletedAudience.Organizer);
    }

    [Fact]
    public async Task NotifyAsync_WithNoContactAtAll_SkipsTheOrganizationRatherThanFailing()
    {
        _fixture.IdentityClient
            .Setup(c => c.GetOrganizationContactAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((IdentityOrganizationContactResponse?)null);

        await _sut.NotifyAsync(Deleted(byPlatformStaff: true));

        VerifyTotalPublished(0);
    }

    // ── degraded Identity ────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task NotifyAsync_WhenIdentityIsUnreachable_StillTellsBuyersTheEventIsCancelled()
    {
        // A buyer learning their event is off without a contact line beats learning nothing at all
        // because a lookup for a phone number failed.
        _fixture.IdentityClient
            .Setup(c => c.GetOrganizationContactAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("Identity down"));
        await SeedTicketAsync("ana@example.com");

        await _sut.NotifyAsync(Deleted());

        VerifyPublishedTo("ana@example.com", ProductDeletedAudience.Buyer);
        VerifyPublished(n => n.OrganizerEmail == null && n.OrganizerPhone == null);
    }

    // ── helpers ──────────────────────────────────────────────────────────────────────────────

    private void GivenContact(string? superAdminEmail = "emir@sarajevo-events.ba") =>
        _fixture.IdentityClient
            .Setup(c => c.GetOrganizationContactAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new IdentityOrganizationContactResponse(
                _organizationId, "Sarajevo Events", "kontakt@sarajevo-events.ba", "+387 33 123 456", superAdminEmail));

    private ProductDeleted Deleted(bool byPlatformStaff = false) =>
        new(_productId, "Ljetni Festival", _organizationId, null, DateTime.UtcNow, byPlatformStaff);

    private void VerifyPublishedTo(string email, ProductDeletedAudience audience) =>
        _fixture.EventPublisher.Verify(
            p => p.PublishAsync(
                EventNames.ProductDeletedNotification,
                It.Is<ProductDeletedNotification>(n =>
                    n.RecipientEmail == email && n.Audience == audience && n.ProductId == _productId),
                It.IsAny<CancellationToken>()),
            Times.Once);

    private void VerifyPublished(Func<ProductDeletedNotification, bool> predicate) =>
        _fixture.EventPublisher.Verify(
            p => p.PublishAsync(
                EventNames.ProductDeletedNotification,
                It.Is<ProductDeletedNotification>(n => predicate(n)),
                It.IsAny<CancellationToken>()),
            Times.Once);

    private void VerifyTotalPublished(int count) =>
        _fixture.EventPublisher.Verify(
            p => p.PublishAsync(
                EventNames.ProductDeletedNotification,
                It.IsAny<ProductDeletedNotification>(),
                It.IsAny<CancellationToken>()),
            Times.Exactly(count));

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
            OrganizationId = _organizationId,
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
