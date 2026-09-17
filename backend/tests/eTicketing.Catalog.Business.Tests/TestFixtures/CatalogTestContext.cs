using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using eTicketing.Catalog.Business.Categories;
using eTicketing.Catalog.Business.Products.Mapping;
using eTicketing.Catalog.Business.Products;
using eTicketing.Catalog.Business.Recommendations;
using eTicketing.Catalog.Data.Repositories;
using eTicketing.Catalog.Data;
using eTicketing.Contracts.Events;
using eTicketing.Contracts.Persistence;
using eTicketing.Shared.Messaging;

namespace eTicketing.Catalog.Business.Tests.TestFixtures;

/// <summary>
/// A real EF Core Sqlite in-memory <see cref="CatalogDbContext"/> wired with the real
/// repositories (not mocked), mirroring
/// eTicketing.Identity.Business.Tests/TestFixtures/IdentityTestContext.cs — the only genuine
/// external system involved in Catalog's business logic is Azure Blob Storage (icons), covered
/// by <see cref="FakeBlobStorageService"/> rather than mocked with Moq, so upload/delete/replace
/// behavior can be asserted for real.
/// </summary>
public sealed class CatalogTestContext : IDisposable
{
    private readonly SqliteConnection _connection;

    public CatalogDbContext DbContext { get; }
    public ICategoryRepository CategoryRepository { get; }
    public IProductRepository ProductRepository { get; }
    public IProductImageRepository ProductImageRepository { get; }
    public IUserInteractionRepository UserInteractionRepository { get; }
    public IRecommendationModelSnapshotRepository ModelSnapshotRepository { get; }
    public IUnitOfWork UnitOfWork { get; }
    public FakeBlobStorageService BlobStorage { get; } = new();

    /// <summary>RabbitMQ is a genuine external system, so it's mocked rather than exercised (see
    /// .claude/rules/10-backend.md). Catalog publishes product.updated through this when a
    /// published product's vital fields change.</summary>
    public Mock<IEventPublisher> EventPublisher { get; } = new();

    /// <summary>The collaborative-filtering model, faked by default so recommendation tests can
    /// state "the model knows nothing about this user" or "the model ranks X above Y" directly,
    /// without training a real factorization on three rows and hoping it agrees. The REAL
    /// MatrixFactorizationModel is exercised separately by MatrixFactorizationModelTests, which is
    /// where training and the blob round trip belong.</summary>
    public FakeRecommendationModel RecommendationModel { get; } = new();

    public RecommendationOptions RecommendationOptions { get; } = new();

    public CatalogTestContext()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        var options = new DbContextOptionsBuilder<CatalogDbContext>()
            .UseSqlite(_connection)
            .AddInterceptors(new AuditableEntitySaveChangesInterceptor())
            .Options;

        DbContext = new CatalogDbContext(options);
        DbContext.Database.EnsureCreated();

        CategoryRepository = new CategoryRepository(DbContext);
        ProductRepository = new ProductRepository(DbContext);
        ProductImageRepository = new ProductImageRepository(DbContext);
        UserInteractionRepository = new UserInteractionRepository(DbContext);
        ModelSnapshotRepository = new RecommendationModelSnapshotRepository(DbContext);
        UnitOfWork = DbContext;
    }

    /// <summary>A second DbContext over the SAME in-memory database — a genuinely independent
    /// writer, which is the only way to reproduce a lost insert race against a unique index for
    /// real instead of mocking the failure. See <see cref="RacingUnitOfWork"/>.</summary>
    public CatalogDbContext NewDbContext() =>
        new(new DbContextOptionsBuilder<CatalogDbContext>()
            .UseSqlite(_connection)
            .AddInterceptors(new AuditableEntitySaveChangesInterceptor())
            .Options);

    public ICategoryService CreateCategoryService() =>
        new CategoryService(CategoryRepository, ProductRepository, UnitOfWork, BlobStorage);


    /// <summary>The real outbox publisher over this fixture's own DbContext, for the tests that
    /// need to see an actual OutboxMessage row rather than a satisfied mock. Every other test uses
    /// the mock, which cannot tell a publish that writes a row from one that writes nothing.</summary>
    public IEventPublisher OutboxPublisher => new OutboxEventPublisher<CatalogDbContext>(DbContext);

    public IProductService CreateProductService(IEventPublisher? eventPublisher = null) =>
        new ProductService(
            ProductRepository, ProductImageRepository, CategoryRepository, UnitOfWork, BlobStorage,
            eventPublisher ?? EventPublisher.Object, CreateResponseFactory());

    public ProductResponseFactory CreateResponseFactory() => new(BlobStorage);

    /// <summary><paramref name="unitOfWork"/> is overridable so a test can slip a
    /// <see cref="RacingUnitOfWork"/> in front of the real one; every other test passes nothing and
    /// gets the plain DbContext.</summary>
    public IRecommendationService CreateRecommendationService(IUnitOfWork? unitOfWork = null) =>
        new RecommendationService(
            UserInteractionRepository, ProductRepository, ModelSnapshotRepository, unitOfWork ?? UnitOfWork,
            RecommendationModel, CreateResponseFactory(), Options.Create(RecommendationOptions),
            NullLogger<RecommendationService>.Instance);

    public PurchaseInteractionRecorder CreatePurchaseInteractionRecorder(IUnitOfWork? unitOfWork = null) =>
        new(UserInteractionRepository, ProductRepository, unitOfWork ?? UnitOfWork,
            NullLogger<PurchaseInteractionRecorder>.Instance);

    public void Dispose()
    {
        DbContext.Dispose();
        _connection.Dispose();
    }
}
