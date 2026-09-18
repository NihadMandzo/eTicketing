using eTicketing.Catalog.Api.Infrastructure.Messaging;
using eTicketing.Catalog.Business.Tests.TestFixtures;
using eTicketing.Catalog.Data;
using eTicketing.Catalog.Data.Entities;
using eTicketing.Contracts.Events;
using eTicketing.Contracts.Persistence;
using eTicketing.Shared.Messaging;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace eTicketing.Catalog.Business.Tests.Recommendations;

/// <summary>
/// The one interaction the inbox introduced that Sqlite cannot answer for: a handler whose own save
/// hits a real unique-index violation, catches it, and retries — while the inbox's transaction is
/// still open around it.
///
/// <para>InteractionRecordingExtensions.RecordOccurrenceAsync has always recovered from losing that
/// race, but it used to do so with no transaction of its own. Whether the transaction survives the
/// caught error and can still commit is the engine's business: SQL Server's default
/// <c>XACT_ABORT OFF</c> aborts the statement, not the transaction, and EF Core's automatic savepoint
/// undoes the failed save. Sqlite in memory cannot stage this at all — a competing writer would need
/// a second connection, and the inbox's open transaction locks the whole database — so these ask for
/// a real server and skip when there isn't one.</para>
///
/// <para>Run with, from <c>backend/</c>:
/// <c>ETICKETING_TEST_SQLSERVER="Server=127.0.0.1,1433;User Id=sa;Password=&lt;SA_PASSWORD&gt;;TrustServerCertificate=True" dotnet test tests/eTicketing.Catalog.Business.Tests</c>
/// </para>
/// </summary>
public class PurchaseInteractionInboxSqlServerTests : IDisposable
{
    private const string Consumer = CatalogRabbitMqConsumerService.QueueName;

    private readonly SqlServerCatalogTestContext? _fixture;
    private readonly Guid _buyer = Guid.NewGuid();
    private Product _product = null!;

    public PurchaseInteractionInboxSqlServerTests()
    {
        // xUnit builds the class even for a skipped test, so without a server there is nothing to
        // connect to and nothing to seed.
        if (string.IsNullOrWhiteSpace(SqlServerFactAttribute.ConnectionString))
            return;

        _fixture = new SqlServerCatalogTestContext();
        SeedAsync().GetAwaiter().GetResult();
    }

    private SqlServerCatalogTestContext Fixture => _fixture!;

    [SqlServerFact]
    public async Task ProcessOnceAsync_WhenTheHandlerLosesTheInsertRace_RecoversInsideTheTransactionAndCommits()
    {
        // The competing row is committed from another connection between the recorder's read and its
        // write, exactly as a concurrent product-detail view would.
        var racing = new RacingUnitOfWork(Fixture.UnitOfWork, InsertPurchaseThroughAnotherWriterAsync);
        var recorder = Fixture.CreatePurchaseInteractionRecorder(racing);
        var inbox = new TransactionalInbox<CatalogDbContext>(Fixture.DbContext);

        var processed = await inbox.ProcessOnceAsync(
            "purchase-race-1", Consumer, ct => recorder.RecordAsync(PurchaseOf(_product), ct));

        processed.Should().BeTrue();
        racing.SaveAttempts.Should().Be(2, "the first save lost the race and the recovery re-ran the upsert");

        // Committed, not rolled back: the caught violation must not have poisoned the transaction.
        await using var verifier = Fixture.NewDbContext();
        var interaction = await verifier.UserInteractions.AsNoTracking()
            .SingleAsync(i => i.UserId == _buyer && i.ProductId == _product.Id);
        interaction.Count.Should().Be(2, "the competing writer's occurrence plus this one");
        (await verifier.InboxMessages.AsNoTracking().CountAsync(m => m.MessageId == "purchase-race-1"))
            .Should().Be(1);
    }

    [SqlServerFact]
    public async Task ProcessOnceAsync_ForTheSameMessageTwice_CountsThePurchaseOnce()
    {
        // The plain path on the real engine, for comparison with the Sqlite test of the same name.
        var recorder = Fixture.CreatePurchaseInteractionRecorder();
        var inbox = new TransactionalInbox<CatalogDbContext>(Fixture.DbContext);
        var purchase = PurchaseOf(_product);

        await inbox.ProcessOnceAsync("purchase-1", Consumer, ct => recorder.RecordAsync(purchase, ct));
        Fixture.DbContext.ChangeTracker.Clear();
        var repeat = await inbox.ProcessOnceAsync("purchase-1", Consumer, ct => recorder.RecordAsync(purchase, ct));

        repeat.Should().BeFalse();

        await using var verifier = Fixture.NewDbContext();
        var interaction = await verifier.UserInteractions.AsNoTracking().SingleAsync(i => i.UserId == _buyer);
        interaction.Count.Should().Be(1);
    }

    private TicketPurchased PurchaseOf(Product product) =>
        new(
            OrderId: Guid.NewGuid(),
            ProductId: product.Id,
            SectorId: Guid.NewGuid(),
            SectorName: "Parter",
            TicketingMode: TicketingMode.SingleOccurrence,
            UserId: _buyer,
            UserEmail: "kupac@example.com",
            TotalPaid: 30m,
            PurchasedAt: DateTime.UtcNow,
            Tickets: [new PurchasedTicket(Guid.NewGuid(), "qr-1", "Odrasli", 30m, null, null, null)]);

    /// <summary>Commits the same (user, product, Purchase) row through a genuinely separate
    /// connection, so the recorder's insert loses to the unique index for real.</summary>
    private async Task InsertPurchaseThroughAnotherWriterAsync()
    {
        await using var other = Fixture.NewDbContext();

        other.UserInteractions.Add(new UserInteraction
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
        await Fixture.CategoryRepository.AddAsync(category);
        await Fixture.UnitOfWork.SaveChangesAsync();

        _product = new Product
        {
            Id = Guid.NewGuid(),
            Name = "Koncert",
            Description = "Koncert",
            OrganizationId = Guid.NewGuid(),
            CategoryId = category.Id,
            City = City.Mostar,
            Date = DateTime.UtcNow.AddDays(10),
            Status = PublishStatus.Published,
            Latitude = 43.34,
            Longitude = 17.81,
        };

        await Fixture.ProductRepository.AddAsync(_product);
        await Fixture.UnitOfWork.SaveChangesAsync();
        Fixture.DbContext.ChangeTracker.Clear();
    }

    public void Dispose() => _fixture?.Dispose();
}
