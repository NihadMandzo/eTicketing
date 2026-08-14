using eTicketing.Catalog.Business.Categories;
using eTicketing.Catalog.Business.Events;
using eTicketing.Catalog.Data;
using eTicketing.Catalog.Data.Repositories;
using eTicketing.Contracts.Persistence;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

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
    public IEventRepository EventRepository { get; }
    public IUnitOfWork UnitOfWork { get; }
    public FakeBlobStorageService BlobStorage { get; } = new();

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
        EventRepository = new EventRepository(DbContext);
        UnitOfWork = DbContext;
    }

    public ICategoryService CreateCategoryService() =>
        new CategoryService(CategoryRepository, EventRepository, UnitOfWork, BlobStorage);

    public IEventService CreateEventService() => new EventService(EventRepository);

    public void Dispose()
    {
        DbContext.Dispose();
        _connection.Dispose();
    }
}
