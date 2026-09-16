using eTicketing.Contracts.Persistence;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using Moq;
using MsOptions = Microsoft.Extensions.Options.Options;
using eTicketing.Contracts.Events;
using eTicketing.Shared.Messaging;
using eTicketing.Shared.TicketPdf;
using eTicketing.Ticketing.Business.Analytics.Anomalies;
using eTicketing.Ticketing.Business.Analytics.Forecasting;
using eTicketing.Ticketing.Business.Analytics.Insights;
using eTicketing.Ticketing.Business.Analytics.Narrative;
using eTicketing.Ticketing.Business.Analytics.Segmentation;
using eTicketing.Ticketing.Business.Analytics;
using eTicketing.Ticketing.Business.External;
using eTicketing.Ticketing.Business.GateDevices;
using eTicketing.Ticketing.Business.Integration;
using eTicketing.Ticketing.Business.Purchases;
using eTicketing.Ticketing.Business.Reports;
using eTicketing.Ticketing.Business.Sectors;
using eTicketing.Ticketing.Business.Security;
using eTicketing.Ticketing.Business.Subscriptions;
using eTicketing.Ticketing.Business.TicketPrint;
using eTicketing.Ticketing.Business.Tickets;
using eTicketing.Ticketing.Business.Time;
using eTicketing.Ticketing.Data.Repositories;
using eTicketing.Ticketing.Data;

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
    public ITicketPrintBatchRepository TicketPrintBatchRepository { get; }
    public IGateDeviceRepository GateDeviceRepository { get; }
    public IUnitOfWork UnitOfWork { get; }
    public Mock<ICatalogClient> CatalogClient { get; } = new();

    /// <summary>Only the Izvještaji reports call Identity; every other org-scoped decision reads
    /// the claim off the token. Mocked like every other cross-service HTTP client here.</summary>
    public Mock<IIdentityClient> IdentityClient { get; } = new();
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

    /// <summary>The real <see cref="PlatformClock"/> over the fake <see cref="Clock"/>, on the same
    /// Europe/Sarajevo zone production runs. Not mocked on purpose: the whole point of the class is
    /// the UTC→local conversion, so a test double would assert nothing. The default pin above is
    /// 10:00 UTC = 12:00 local, comfortably mid-day, so tests that don't care about the boundary
    /// read the same date either way; the boundary tests move the clock to 22:30 UTC themselves.</summary>
    public PlatformClock PlatformClock =>
        new(Clock, MsOptions.Create(new PlatformTimeOptions { TimeZoneId = "Europe/Sarajevo" }));

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
        TicketPrintBatchRepository = new TicketPrintBatchRepository(DbContext);
        GateDeviceRepository = new GateDeviceRepository(DbContext);
        UnitOfWork = DbContext;

        ValidationLock
            .Setup(l => l.TryAcquireAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("test-lock-token");

        // Reports label their rows with organization names, but no report *depends* on the label
        // being resolvable — an unknown id falls back to a placeholder. Defaulting to an empty
        // list keeps every test that isn't about labelling free of Identity setup.
        IdentityClient
            .Setup(c => c.GetOrganizationsAsync(It.IsAny<IReadOnlyList<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        CatalogClient
            .Setup(c => c.GetOrganizationProductStatsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);
    }

    public TicketResponseFactory ResponseFactory => new(QrCodec);

    public ISectorService CreateSectorService() =>
        new SectorService(SectorRepository, CatalogClient.Object, CapacityLock.Object, UnitOfWork);

    public ITicketTypeService CreateTicketTypeService() =>
        new TicketTypeService(SectorRepository, TicketTypeRepository, UnitOfWork);

    public ITicketPdfService CreateTicketPdfService() =>
        new TicketPdfService(
            TicketRepository,
            CatalogClient.Object,
            QrCodec,
            MsOptions.Create(new TicketSupportOptions { Email = "podrska@ekarta.ba", Phone = "+387 33 555 120" }),
            NullLogger<TicketPdfService>.Instance);

    public ITicketService CreateTicketService() =>
        new TicketService(TicketRepository, ResponseFactory);

    public ISubscriptionService CreateSubscriptionService() =>
        new SubscriptionService(
            SubscriptionRepository, PaymentClient.Object, UnitOfWork, NullLogger<SubscriptionService>.Instance);

    public ISubscriptionRenewalService CreateSubscriptionRenewalService() =>
        new SubscriptionRenewalService(
            SubscriptionRepository, TicketRepository, SectorRepository, CapacityLock.Object,
            EventPublisher.Object, UnitOfWork, QrCodec, PlatformClock, NullLogger<SubscriptionRenewalService>.Instance);

    public ITicketValidationService CreateTicketValidationService() =>
        new TicketValidationService(
            TicketRepository, ValidationLock.Object, CatalogClient.Object, QrCodec, UnitOfWork, PlatformClock,
            NullLogger<TicketValidationService>.Instance);

    public ITicketPdfCompletionService CreateTicketPdfCompletionService() =>
        new TicketPdfCompletionService(TicketRepository, UnitOfWork, NullLogger<TicketPdfCompletionService>.Instance);

    public IProductChangeNotifier CreateProductChangeNotifier() =>
        new ProductChangeNotifier(
            TicketRepository, EventPublisher.Object, UnitOfWork, PlatformClock,
            NullLogger<ProductChangeNotifier>.Instance);

    public IProductDeletionNotifier CreateProductDeletionNotifier() =>
        new ProductDeletionNotifier(
            TicketRepository, IdentityClient.Object, EventPublisher.Object, UnitOfWork, PlatformClock,
            NullLogger<ProductDeletionNotifier>.Instance);

    /// <summary>Records what the service asked to render without doing any of it, so a print test
    /// can assert on the queue hand-off and drive the renderer itself when it wants to.</summary>
    public RecordingTicketPrintQueue PrintQueue { get; } = new();

    public ITicketPrintService CreateTicketPrintService() =>
        new TicketPrintService(
            SectorRepository, TicketRepository, TicketPrintBatchRepository, CapacityLock.Object,
            CatalogClient.Object, PrintQueue, UnitOfWork, PlatformClock,
            NullLogger<TicketPrintService>.Instance);

    public ITicketPrintRenderer CreateTicketPrintRenderer() =>
        new TicketPrintRenderer(
            TicketPrintBatchRepository, TicketRepository, CatalogClient.Object, QrCodec,
            MsOptions.Create(new TicketSupportOptions { Email = "podrska@ekarta.ba", Phone = "+387 33 555 120" }),
            UnitOfWork, PlatformClock, NullLogger<TicketPrintRenderer>.Instance);

    public IReportService CreateReportService() =>
        new ReportService(TicketRepository, SectorRepository, CatalogClient.Object, IdentityClient.Object, PlatformClock);

    /// <summary>The AI Uvidi service with the real ML.NET components — SSA and K-Means are
    /// milliseconds on test-sized data, and stubbing them would leave the source ladder (Model /
    /// Heuristic / Insufficient) untested where it matters most, in the service that reports it.
    /// Only the narrative writer is swappable, since the whole point of that seam is that a test
    /// runs with no model server anywhere near it.</summary>
    public IAnalyticsService CreateAnalyticsService(INarrativeWriter? narrative = null) =>
        new AnalyticsService(
            CreateReportService(),
            TicketRepository,
            new SsaSalesForecaster(NullLogger<SsaSalesForecaster>.Instance),
            new SsaAnomalyDetector(NullLogger<SsaAnomalyDetector>.Instance),
            new KMeansAudienceSegmenter(NullLogger<KMeansAudienceSegmenter>.Instance),
            new InsightGenerator(),
            narrative ?? new NullNarrativeWriter(),
            PlatformClock,
            // A fresh cache per service, so one test's response can never be served to another.
            new MemoryCache(new MemoryCacheOptions()),
            MsOptions.Create(new InsightsOptions()),
            NullLogger<AnalyticsService>.Instance);

    public IReportPdfService CreateReportPdfService() =>
        new ReportPdfService(CreateReportService(), CreateAnalyticsService(), PlatformClock);
    /// <summary>Real, not mocked — it is CSPRNG + SHA-256, and the tests need keys the
    /// authentication path would genuinely accept back.</summary>
    public GateDeviceKeyGenerator KeyGenerator { get; } = new();

    public IGateDeviceService CreateGateDeviceService() =>
        new GateDeviceService(
            GateDeviceRepository, CatalogClient.Object, KeyGenerator, UnitOfWork, Clock,
            NullLogger<GateDeviceService>.Instance);


    /// <summary>The real outbox publisher over this fixture's own DbContext, for the tests that
    /// need to see an actual OutboxMessage row rather than a satisfied mock. Every other test uses
    /// the mock, which cannot tell a publish that writes a row from one that writes nothing.</summary>
    public IEventPublisher OutboxPublisher => new OutboxEventPublisher<TicketingDbContext>(DbContext);

    public IPurchaseService CreatePurchaseService(IEventPublisher? eventPublisher = null) =>
        new PurchaseService(
            SectorRepository, TicketRepository, SubscriptionRepository, CapacityLock.Object, PaymentClient.Object,
            eventPublisher ?? EventPublisher.Object, UnitOfWork, QrCodec, ResponseFactory, PlatformClock,
            NullLogger<PurchaseService>.Instance);

    public void Dispose()
    {
        DbContext.Dispose();
        _connection.Dispose();
    }
}

/// <summary>The real queue is a Channel drained by a hosted service; tests only need to know which
/// batch ids were handed to it.</summary>
public sealed class RecordingTicketPrintQueue : ITicketPrintQueue
{
    public List<Guid> Enqueued { get; } = [];

    public void Enqueue(Guid batchId) => Enqueued.Add(batchId);
}
