using eTicketing.Catalog.Business.Recommendations;
using eTicketing.Catalog.Api.Infrastructure.Messaging;
using eTicketing.Catalog.Business.Tests.TestFixtures;
using eTicketing.Catalog.Data;
using eTicketing.Catalog.Data.Entities;
using eTicketing.Contracts.Events;
using eTicketing.Contracts.Persistence;
using eTicketing.Shared.Messaging;
using FluentAssertions;

namespace eTicketing.Catalog.Business.Tests.Recommendations;

/// <summary>
/// The TicketPurchased handler, tested without a broker — which is the reason it is a plain
/// business class instead of logic living inside CatalogRabbitMqConsumerService.
/// </summary>
public class PurchaseInteractionRecorderTests : IDisposable
{
    /// <summary>The queue name the consumer records processed messages under — taken from the
    /// consumer itself rather than repeated here, so the two cannot drift apart.</summary>
    private const string Consumer = CatalogRabbitMqConsumerService.QueueName;

    private readonly CatalogTestContext _fixture = new();
    private readonly PurchaseInteractionRecorder _sut;

    private readonly Guid _buyer = Guid.NewGuid();
    private Product _product = null!;

    public PurchaseInteractionRecorderTests()
    {
        _sut = _fixture.CreatePurchaseInteractionRecorder();
        SeedAsync().GetAwaiter().GetResult();
    }

    [Fact]
    public async Task RecordAsync_ForATicketPurchasedEvent_RecordsAPurchaseInteraction()
    {
        await _sut.RecordAsync(PurchaseOf(_product));

        var history = await _fixture.UserInteractionRepository.GetByUserAsync(_buyer);

        var interaction = history.Should().ContainSingle().Subject;
        interaction.ProductId.Should().Be(_product.Id);
        interaction.Type.Should().Be(InteractionType.Purchase);
        interaction.Count.Should().Be(1);
    }

    [Fact]
    public async Task RecordAsync_ForTheSameOrderTwice_DoesNotDuplicateTheRow()
    {
        // RabbitMQ is at-least-once, so a redelivery is normal operation, not an anomaly.
        var purchase = PurchaseOf(_product);

        await _sut.RecordAsync(purchase);
        await _sut.RecordAsync(purchase);

        var history = await _fixture.UserInteractionRepository.GetByUserAsync(_buyer);
        history.Should().ContainSingle("the upsert is keyed on (user, product, type), so a redelivery cannot duplicate a row");
    }

    [Fact]
    public async Task RecordAsync_ForTheSameOrderTwice_OnItsOwnCountsItTwice()
    {
        // Pinned on purpose: this is why the consumer runs the recorder through the inbox. The row is
        // not duplicated, but its counter is bumped again — so the method alone is not idempotent.
        var purchase = PurchaseOf(_product);

        await _sut.RecordAsync(purchase);
        await _sut.RecordAsync(purchase);

        var history = await _fixture.UserInteractionRepository.GetByUserAsync(_buyer);
        history.Should().ContainSingle().Which.Count.Should().Be(2);
    }

    [Fact]
    public async Task RecordAsync_RedeliveredThroughTheInbox_CountsThePurchaseOnce()
    {
        // The path CatalogRabbitMqConsumerService takes: the same message id twice.
        var inbox = new TransactionalInbox<CatalogDbContext>(_fixture.DbContext);
        var purchase = PurchaseOf(_product);

        var first = await inbox.ProcessOnceAsync("purchase-1", Consumer, ct => _sut.RecordAsync(purchase, ct));
        _fixture.DbContext.ChangeTracker.Clear();
        var repeat = await inbox.ProcessOnceAsync("purchase-1", Consumer, ct => _sut.RecordAsync(purchase, ct));

        first.Should().BeTrue();
        repeat.Should().BeFalse();
        var history = await _fixture.UserInteractionRepository.GetByUserAsync(_buyer);
        history.Should().ContainSingle().Which.Count.Should().Be(1);
    }

    [Fact]
    public async Task RecordAsync_ForTwoDifferentPurchasesOfTheSameProduct_ThroughTheInbox_CountsBoth()
    {
        // A buyer who really did buy twice is a stronger signal, and must stay one.
        var inbox = new TransactionalInbox<CatalogDbContext>(_fixture.DbContext);

        await inbox.ProcessOnceAsync("purchase-1", Consumer, ct => _sut.RecordAsync(PurchaseOf(_product), ct));
        _fixture.DbContext.ChangeTracker.Clear();
        await inbox.ProcessOnceAsync("purchase-2", Consumer, ct => _sut.RecordAsync(PurchaseOf(_product), ct));

        var history = await _fixture.UserInteractionRepository.GetByUserAsync(_buyer);
        history.Should().ContainSingle().Which.Count.Should().Be(2);
    }

    [Fact]
    public async Task RecordAsync_WhenAConcurrentWriterInsertedTheSameRowFirst_IncrementsInsteadOfFailing()
    {
        // The consumer competes with the buyer's own product-detail view for the same
        // (user, product) key — a redelivered event alongside a live view is enough. Failing here
        // would send the message back around the retry loop over a row that already exists.
        var racing = new RacingUnitOfWork(_fixture.UnitOfWork, InsertPurchaseThroughAnotherWriterAsync);
        var sut = _fixture.CreatePurchaseInteractionRecorder(racing);

        await sut.RecordAsync(PurchaseOf(_product));

        racing.SaveAttempts.Should().Be(2, "the first commit lost the race and the recovery re-ran the upsert");

        var history = await _fixture.UserInteractionRepository.GetByUserAsync(_buyer);
        history.Should().ContainSingle().Which.Count.Should().Be(2);
    }

    [Fact]
    public async Task RecordAsync_ForAnUnknownProduct_SkipsInsteadOfThrowing()
    {
        // A product deleted between purchase and delivery. Throwing would send the message around
        // the retry loop forever over a row that will never exist again.
        var purchase = PurchaseOf(_product) with { ProductId = Guid.NewGuid() };

        var record = async () => await _sut.RecordAsync(purchase);

        await record.Should().NotThrowAsync();
        (await _fixture.UserInteractionRepository.GetByUserAsync(_buyer)).Should().BeEmpty();
    }

    [Fact]
    public async Task RecordAsync_ForADraftProduct_StillRecordsIt()
    {
        // Unlike a view, a purchase is a fact that already happened — if a product was somehow
        // unpublished after being bought, the buyer's history is still real and still useful
        // training signal.
        var draft = await AddProductAsync("Nacrt", PublishStatus.Draft);

        await _sut.RecordAsync(PurchaseOf(draft));

        (await _fixture.UserInteractionRepository.GetByUserAsync(_buyer)).Should().ContainSingle();
    }

    [Fact]
    public async Task RecordAsync_ForTwoDifferentProducts_RecordsThemSeparately()
    {
        var second = await AddProductAsync("Drugi proizvod");

        await _sut.RecordAsync(PurchaseOf(_product));
        await _sut.RecordAsync(PurchaseOf(second));

        var history = await _fixture.UserInteractionRepository.GetByUserAsync(_buyer);
        history.Should().HaveCount(2);
    }

    /// <summary>One order minting three tickets is still one purchase signal — TicketPurchased is
    /// published per order, and interest in a product does not triple because somebody brought
    /// friends.</summary>
    private TicketPurchased PurchaseOf(Product product) =>
        new(
            OrderId: Guid.NewGuid(),
            ProductId: product.Id,
            SectorId: Guid.NewGuid(),
            SectorName: "Parter",
            TicketingMode: TicketingMode.SingleOccurrence,
            UserId: _buyer,
            UserEmail: "kupac@example.com",
            TotalPaid: 90m,
            PurchasedAt: DateTime.UtcNow,
            Tickets:
            [
                new PurchasedTicket(Guid.NewGuid(), "qr-1", "Odrasli", 30m, null, null, null),
                new PurchasedTicket(Guid.NewGuid(), "qr-2", "Odrasli", 30m, null, null, null),
                new PurchasedTicket(Guid.NewGuid(), "qr-3", "Djeca", 30m, null, null, null),
            ]);

    /// <summary>Commits the same (user, product, Purchase) row through a genuinely separate
    /// DbContext, so the recorder's own insert loses to the unique index for real.</summary>
    private async Task InsertPurchaseThroughAnotherWriterAsync()
    {
        await using var other = _fixture.NewDbContext();

        other.Add(new UserInteraction
        {
            Id = Guid.NewGuid(),
            UserId = _buyer,
            ProductId = _product.Id,
            Type = InteractionType.Purchase,
            Count = 1,
            LastOccurredAt = DateTime.UtcNow,
        });

        await other.SaveChangesAsync();
    }

    private async Task SeedAsync()
    {
        var category = new Category { Name = "Pozorište", IsActive = true, TicketingMode = TicketingMode.SingleOccurrence };
        await _fixture.CategoryRepository.AddAsync(category);
        await _fixture.UnitOfWork.SaveChangesAsync();

        _product = await AddProductAsync("Koncert");
    }

    private async Task<Product> AddProductAsync(string name, PublishStatus status = PublishStatus.Published)
    {
        var categoryId = _fixture.DbContext.Categories.First().Id;

        var product = new Product
        {
            Id = Guid.NewGuid(),
            Name = name,
            Description = name,
            OrganizationId = Guid.NewGuid(),
            CategoryId = categoryId,
            City = City.Mostar,
            Date = DateTime.UtcNow.AddDays(10),
            Status = status,
            Latitude = 43.34,
            Longitude = 17.81,
        };

        await _fixture.ProductRepository.AddAsync(product);
        await _fixture.UnitOfWork.SaveChangesAsync();
        return product;
    }

    public void Dispose() => _fixture.Dispose();
}
