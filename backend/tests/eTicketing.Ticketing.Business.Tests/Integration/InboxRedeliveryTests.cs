using System.Text.Json;
using eTicketing.Contracts.Events;
using eTicketing.Contracts.Persistence;
using eTicketing.Shared.Messaging;
using eTicketing.Ticketing.Api.Infrastructure.Messaging;
using eTicketing.Ticketing.Business.Integration;
using eTicketing.Ticketing.Business.ReadModels;
using eTicketing.Ticketing.Business.Tests.TestFixtures;
using eTicketing.Ticketing.Data;
using eTicketing.Ticketing.Data.Entities;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace eTicketing.Ticketing.Business.Tests.Integration;

/// <summary>
/// The duplicate the inbox exists for: a redelivered product.updated or product.deleted used to run
/// its fan-out again and write a second set of per-buyer notifications, under fresh ids nothing
/// downstream could recognise. Driven through <see cref="TicketingMessageRouter"/> — the same path the
/// consumer takes — with the real inbox, the real notifiers and the real outbox publisher, so what is
/// asserted is the outbox rows that would actually reach the broker.
/// </summary>
public class InboxRedeliveryTests : IDisposable
{
    private readonly TicketingTestContext _fixture = new();
    private readonly Guid _productId = Guid.NewGuid();
    private readonly Guid _organizationId = Guid.NewGuid();

    /// <summary>Swapped by the one test that needs the deletion notices to fail first.</summary>
    private IProductDeletionNotifier _deletionNotifier;

    public InboxRedeliveryTests()
    {
        _deletionNotifier = CreateDeletionNotifier();
    }

    private OutboxEventPublisher<TicketingDbContext> OutboxPublisher => new(_fixture.DbContext);

    private IProductDeletionNotifier CreateDeletionNotifier() =>
        new ProductDeletionNotifier(
            _fixture.TicketRepository, _fixture.OrganizationSnapshotRepository, OutboxPublisher,
            _fixture.UnitOfWork, _fixture.PlatformClock, NullLogger<ProductDeletionNotifier>.Instance);

    /// <summary>Stands in for one message's DI scope: the services the router resolves, all over the
    /// fixture's single context, as a real scope's would share one.</summary>
    private IServiceProvider Scope()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IInbox>(new TransactionalInbox<TicketingDbContext>(_fixture.DbContext));
        services.AddSingleton(_fixture.CreateProductSnapshotProjector());
        services.AddSingleton<IProductChangeNotifier>(new ProductChangeNotifier(
            _fixture.TicketRepository, OutboxPublisher, _fixture.UnitOfWork, _fixture.PlatformClock,
            NullLogger<ProductChangeNotifier>.Instance));
        services.AddSingleton(_deletionNotifier);
        return services.BuildServiceProvider();
    }

    /// <summary>One delivery. The change tracker is cleared first because production gives every
    /// message a fresh scope, and a test that leaned on entities left over from the previous delivery
    /// would be testing something that cannot happen.</summary>
    private async Task<bool> DeliverAsync<T>(string routingKey, T message, string? messageId)
    {
        _fixture.DbContext.ChangeTracker.Clear();
        return await TicketingMessageRouter.RouteAsync(
            Scope(), routingKey, messageId, JsonSerializer.SerializeToUtf8Bytes(message), CancellationToken.None);
    }

    private ProductUpdated Updated() =>
        new(_productId, "Ljetni Festival", DateTime.UtcNow, [new ProductFieldChange("Naziv", "Staro", "Novo")]);

    private ProductDeleted Deleted() =>
        new(_productId, "Ljetni Festival", _organizationId, null, DateTime.UtcNow, false);

    private Task<int> OutboxRowsAsync(string routingKey) =>
        _fixture.DbContext.OutboxMessages.AsNoTracking().CountAsync(m => m.RoutingKey == routingKey);

    private Task<bool> SnapshotExistsAsync() =>
        _fixture.DbContext.ProductSnapshots.AsNoTracking().AnyAsync(s => s.ProductId == _productId);

    private Task<int> InboxRecordsAsync() => _fixture.DbContext.InboxMessages.AsNoTracking().CountAsync();

    private async Task SeedBuyerAsync(string email)
    {
        var sector = new Sector
        {
            Id = Guid.NewGuid(),
            ProductId = _productId,
            OrganizationId = _organizationId,
            Name = "VIP",
            Capacity = 100,
            Price = 50,
            Status = PublishStatus.Published,
            TicketingMode = TicketingMode.SingleOccurrence,
        };
        _fixture.DbContext.Sectors.Add(sector);
        _fixture.DbContext.Tickets.Add(
            Ticket.ForSingleOccurrence(sector.Id, null, Guid.NewGuid(), _productId, Guid.NewGuid(), email, 50));

        await _fixture.DbContext.SaveChangesAsync();
        _fixture.DbContext.ChangeTracker.Clear();
    }

    // ── product.updated ──────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task RouteAsync_ForProductUpdatedDeliveredTwice_NotifiesEachBuyerOnce()
    {
        await SeedBuyerAsync("ana@example.com");
        await SeedBuyerAsync("marko@example.com");

        var first = await DeliverAsync(EventNames.ProductUpdated, Updated(), "update-1");
        var repeat = await DeliverAsync(EventNames.ProductUpdated, Updated(), "update-1");

        first.Should().BeTrue();
        repeat.Should().BeFalse();
        (await OutboxRowsAsync(EventNames.ProductChanged)).Should().Be(2, "one notice per buyer, not one per delivery");
    }

    [Fact]
    public async Task RouteAsync_ForTwoDifferentProductUpdates_NotifiesBuyersOfEach()
    {
        // Two genuine edits are two messages. Recognising repeats must not swallow the second.
        await SeedBuyerAsync("ana@example.com");

        await DeliverAsync(EventNames.ProductUpdated, Updated(), "update-1");
        await DeliverAsync(EventNames.ProductUpdated, Updated(), "update-2");

        (await OutboxRowsAsync(EventNames.ProductChanged)).Should().Be(2);
    }

    // ── product.deleted ──────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task RouteAsync_ForProductDeletedDeliveredTwice_RemovesTheSnapshotAndNotifiesEachBuyerOnce()
    {
        await _fixture.SeedProductSnapshotAsync(_productId, organizationId: _organizationId);
        await SeedBuyerAsync("ana@example.com");
        await SeedBuyerAsync("marko@example.com");

        await DeliverAsync(EventNames.ProductDeleted, Deleted(), "delete-1");
        var repeat = await DeliverAsync(EventNames.ProductDeleted, Deleted(), "delete-1");

        repeat.Should().BeFalse();
        (await SnapshotExistsAsync()).Should().BeFalse();
        (await OutboxRowsAsync(EventNames.ProductDeletedNotification)).Should().Be(2);
    }

    [Fact]
    public async Task RouteAsync_WhenTheDeletionNoticesFail_StillCommitsTheSnapshotRemoval()
    {
        // Dropping the snapshot is what stops the deleted product's sectors selling, so it must not
        // be rolled back with a failed fan-out — the retry path is one attempt and then the
        // dead-letter queue, and the product would stay on sale until someone replayed it by hand.
        await _fixture.SeedProductSnapshotAsync(_productId, organizationId: _organizationId);
        await SeedBuyerAsync("ana@example.com");

        var failing = new Mock<IProductDeletionNotifier>();
        failing.Setup(n => n.NotifyAsync(It.IsAny<ProductDeleted>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("slanje obavještenja nije uspjelo"));
        _deletionNotifier = failing.Object;

        var deliver = () => DeliverAsync(EventNames.ProductDeleted, Deleted(), "delete-1");

        await deliver.Should().ThrowAsync<InvalidOperationException>();
        (await SnapshotExistsAsync()).Should().BeFalse();
        (await InboxRecordsAsync()).Should().Be(0, "the message did not take full effect, so the retry must run it");
    }

    [Fact]
    public async Task RouteAsync_ForProductDeletedRetriedAfterItsNoticesFailed_NotifiesOnce()
    {
        await _fixture.SeedProductSnapshotAsync(_productId, organizationId: _organizationId);
        await SeedBuyerAsync("ana@example.com");

        // First attempt: the fan-out writes its row and then fails, so the row must not survive.
        var real = CreateDeletionNotifier();
        var failing = new Mock<IProductDeletionNotifier>();
        failing.Setup(n => n.NotifyAsync(It.IsAny<ProductDeleted>(), It.IsAny<CancellationToken>()))
            .Returns(async (ProductDeleted message, CancellationToken ct) =>
            {
                await real.NotifyAsync(message, ct);
                throw new InvalidOperationException("greška nakon fan-outa");
            });
        _deletionNotifier = failing.Object;

        var firstAttempt = () => DeliverAsync(EventNames.ProductDeleted, Deleted(), "delete-1");
        await firstAttempt.Should().ThrowAsync<InvalidOperationException>();
        (await OutboxRowsAsync(EventNames.ProductDeletedNotification)).Should().Be(0);

        // The consumer's retry.
        _deletionNotifier = CreateDeletionNotifier();
        var retried = await DeliverAsync(EventNames.ProductDeleted, Deleted(), "delete-1");

        retried.Should().BeTrue();
        (await OutboxRowsAsync(EventNames.ProductDeletedNotification)).Should().Be(1);
    }

    // ── routing ──────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task RouteAsync_WithoutAMessageId_ProcessesEveryDelivery()
    {
        // A message with nothing to recognise it by is processed as it was before the inbox existed.
        await SeedBuyerAsync("ana@example.com");

        await DeliverAsync(EventNames.ProductUpdated, Updated(), messageId: null);
        await DeliverAsync(EventNames.ProductUpdated, Updated(), messageId: null);

        (await OutboxRowsAsync(EventNames.ProductChanged)).Should().Be(2);
        (await InboxRecordsAsync()).Should().Be(0);
    }

    [Fact]
    public async Task RouteAsync_ForAnUnknownRoutingKey_ThrowsAndRecordsNothing()
    {
        var deliver = () => DeliverAsync("nepoznat.dogadjaj", new { }, "unknown-1");

        await deliver.Should().ThrowAsync<InvalidOperationException>();
        (await InboxRecordsAsync()).Should().Be(0);
    }

    public void Dispose() => _fixture.Dispose();
}
