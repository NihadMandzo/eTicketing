using eTicketing.Contracts.Persistence;
using eTicketing.Ticketing.Business.External;
using eTicketing.Ticketing.Business.Integration;
using eTicketing.Ticketing.Business.Purchases;
using eTicketing.Ticketing.Business.Sectors;
using eTicketing.Ticketing.Business.Tickets;
using eTicketing.Ticketing.Data;
using eTicketing.Ticketing.Data.Repositories;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using Moq;

namespace eTicketing.Ticketing.Business.Tests.TestFixtures;

/// <summary>
/// A real EF Core Sqlite in-memory <see cref="TicketingDbContext"/> wired with the real
/// repositories (not mocked), mirroring
/// eTicketing.Identity.Business.Tests/TestFixtures/IdentityTestContext.cs and
/// eTicketing.Catalog.Business.Tests/TestFixtures/CatalogTestContext.cs. The genuine external
/// systems — the HTTP call to eTicketing.Catalog, the HTTP call to eTicketing.Payment, the
/// Redis-backed capacity and validation locks, and the RabbitMQ event publisher — are mocked with
/// Moq rather than exercised for real, per .claude/rules/10-backend.md.
/// </summary>
public sealed class TicketingTestContext : IDisposable
{
    /// <summary>Any 32+ character string works — the codec only ever HMACs with it, and both the
    /// signing and the verifying side of every test share this one instance.</summary>
    private const string QrSigningKey = "test-qr-signing-key-min-32-characters-long";

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

    /// <summary>Defaults to granting the lock so the vast majority of validation tests don't have
    /// to set it up; contention tests override it with a null return.</summary>
    public Mock<ITicketValidationLock> ValidationLock { get; } = new();

    /// <summary>Real (not mocked) — it's a pure function of a key, and tests need to produce
    /// payloads the service under test will actually accept.</summary>
    public TicketQrCodec QrCodec { get; } = new(QrSigningKey);

    /// <summary>Every "is this ticket valid today" assertion pivots on the current date, so the
    /// clock is injected and starts pinned. Tests move it with <c>Clock.SetUtcNow(...)</c> rather
    /// than depending on the day the suite happens to run.</summary>
    public FakeTimeProvider Clock { get; } = new(new DateTimeOffset(2026, 8, 24, 10, 0, 0, TimeSpan.Zero));

    public FakeBlobStorageService BlobStorage { get; } = new();

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

        ValidationLock
            .Setup(l => l.TryAcquireAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("test-lock-token");
    }

    public TicketResponseFactory ResponseFactory => new(QrCodec, BlobStorage);

    public ISectorService CreateSectorService() =>
        new SectorService(SectorRepository, CatalogClient.Object, CapacityLock.Object, UnitOfWork);

    public ITicketTypeService CreateTicketTypeService() =>
        new TicketTypeService(SectorRepository, TicketTypeRepository, UnitOfWork);

    public ITicketService CreateTicketService() =>
        new TicketService(TicketRepository, ResponseFactory);

    public ITicketValidationService CreateTicketValidationService() =>
        new TicketValidationService(
            TicketRepository, ValidationLock.Object, CatalogClient.Object, QrCodec, UnitOfWork, Clock,
            NullLogger<TicketValidationService>.Instance);

    public ITicketPdfCompletionService CreateTicketPdfCompletionService() =>
        new TicketPdfCompletionService(TicketRepository, UnitOfWork, NullLogger<TicketPdfCompletionService>.Instance);

    public IProductChangeNotifier CreateProductChangeNotifier() =>
        new ProductChangeNotifier(TicketRepository, EventPublisher.Object, Clock, NullLogger<ProductChangeNotifier>.Instance);

    public IPurchaseService CreatePurchaseService() =>
        new PurchaseService(
            SectorRepository, TicketRepository, SubscriptionRepository, CapacityLock.Object, PaymentClient.Object,
            EventPublisher.Object, UnitOfWork, QrCodec, ResponseFactory, NullLogger<PurchaseService>.Instance);

    public void Dispose()
    {
        DbContext.Dispose();
        _connection.Dispose();
    }
}
