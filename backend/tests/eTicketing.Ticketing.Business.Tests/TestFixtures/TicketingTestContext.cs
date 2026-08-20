using eTicketing.Contracts.Persistence;
using eTicketing.Ticketing.Business.External;
using eTicketing.Ticketing.Business.Purchases;
using eTicketing.Ticketing.Business.Sectors;
using eTicketing.Ticketing.Business.Tickets;
using eTicketing.Ticketing.Data;
using eTicketing.Ticketing.Data.Repositories;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Moq;

namespace eTicketing.Ticketing.Business.Tests.TestFixtures;

/// <summary>
/// A real EF Core Sqlite in-memory <see cref="TicketingDbContext"/> wired with the real
/// repositories (not mocked), mirroring
/// eTicketing.Identity.Business.Tests/TestFixtures/IdentityTestContext.cs and
/// eTicketing.Catalog.Business.Tests/TestFixtures/CatalogTestContext.cs. The genuine external
/// systems — the HTTP call to eTicketing.Catalog, the HTTP call to eTicketing.Payment, the
/// Redis-backed capacity lock, and the RabbitMQ event publisher — are mocked with Moq rather than
/// exercised for real, per .claude/rules/10-backend.md.
/// </summary>
public sealed class TicketingTestContext : IDisposable
{
    private readonly SqliteConnection _connection;

    public TicketingDbContext DbContext { get; }
    public ISectorRepository SectorRepository { get; }
    public ITicketTypeRepository TicketTypeRepository { get; }
    public ITicketRepository TicketRepository { get; }
    public ISubscriptionRepository SubscriptionRepository { get; }
    public IUnitOfWork UnitOfWork { get; }
    public Mock<ICatalogClient> CatalogClient { get; } = new();
    public Mock<ISectorCapacityLock> CapacityLock { get; } = new();
    public Mock<IPaymentClient> PaymentClient { get; } = new();
    public Mock<IEventPublisher> EventPublisher { get; } = new();

    public TicketingTestContext()
    {
        // "Foreign Keys=True" (SqliteConnectionStringBuilder.ForeignKeys) is the documented way to
        // turn on FK enforcement for Microsoft.Data.Sqlite — a bare "PRAGMA foreign_keys=ON;"
        // issued after Open() does not reliably stick. Without this, every OnDelete(Restrict) FK in
        // the model (TicketType->Sector, Ticket->TicketType, etc.) would silently no-op on a
        // violating delete instead of throwing, unlike the real SQL Server behind every other
        // environment.
        _connection = new SqliteConnection("DataSource=:memory:;Foreign Keys=True");
        _connection.Open();

        var options = new DbContextOptionsBuilder<TicketingDbContext>()
            .UseSqlite(_connection)
            .AddInterceptors(new AuditableEntitySaveChangesInterceptor())
            .Options;

        DbContext = new TicketingDbContext(options);
        DbContext.Database.EnsureCreated();

        SectorRepository = new SectorRepository(DbContext);
        TicketTypeRepository = new TicketTypeRepository(DbContext);
        TicketRepository = new TicketRepository(DbContext);
        SubscriptionRepository = new SubscriptionRepository(DbContext);
        UnitOfWork = DbContext;
    }

    public ISectorService CreateSectorService() =>
        new SectorService(SectorRepository, CatalogClient.Object, CapacityLock.Object, UnitOfWork);

    public ITicketTypeService CreateTicketTypeService() =>
        new TicketTypeService(SectorRepository, TicketTypeRepository, UnitOfWork);

    public ITicketService CreateTicketService() =>
        new TicketService(TicketRepository);

    public IPurchaseService CreatePurchaseService() =>
        new PurchaseService(SectorRepository, TicketRepository, SubscriptionRepository, CapacityLock.Object, PaymentClient.Object, EventPublisher.Object, UnitOfWork);

    public void Dispose()
    {
        DbContext.Dispose();
        _connection.Dispose();
    }
}
