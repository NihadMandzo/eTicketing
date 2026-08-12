using eTicketing.Catalog.Business;
using eTicketing.Catalog.Business.Categories;
using eTicketing.Catalog.Business.Events;
using eTicketing.Catalog.Data;
using eTicketing.Catalog.Data.Repositories;
using eTicketing.Contracts.Persistence;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace eTicketing.Catalog.Business.Tests.TestFixtures;

/// <summary>
/// A real EF Core Sqlite in-memory <see cref="CatalogDbContext"/> wired with the real
/// repositories (not mocked), mirroring
/// eTicketing.Identity.Business.Tests/TestFixtures/IdentityTestContext.cs — no genuine
/// external system (RabbitMQ, HTTP client to another service) is involved in Catalog's
/// business logic for this feature, so nothing needs mocking here at all.
/// </summary>
public sealed class CatalogTestContext : IDisposable
{
    private readonly SqliteConnection _connection;

    public CatalogDbContext DbContext { get; }
    public ICategoryRepository CategoryRepository { get; }
    public IEventRepository EventRepository { get; }
    public IUnitOfWork UnitOfWork { get; }
    public CatalogOptions CatalogOptions { get; } = new() { PublicBaseUrl = "http://localhost:5000/api" };

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
        new CategoryService(CategoryRepository, UnitOfWork, Options.Create(CatalogOptions));

    public IEventService CreateEventService() => new EventService(EventRepository);

    public void Dispose()
    {
        DbContext.Dispose();
        _connection.Dispose();
    }
}
