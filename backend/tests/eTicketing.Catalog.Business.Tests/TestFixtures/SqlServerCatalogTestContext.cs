using eTicketing.Catalog.Business.Recommendations;
using eTicketing.Catalog.Data;
using eTicketing.Catalog.Data.Repositories;
using eTicketing.Contracts.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace eTicketing.Catalog.Business.Tests.TestFixtures;

/// <summary>
/// The same shape as <see cref="CatalogTestContext"/>, but on a real SQL Server: a database of its
/// own, created on construction and dropped on dispose, so a run leaves nothing behind and two runs
/// cannot collide.
///
/// <para>Used only by the tests that genuinely need the engine (see
/// <see cref="SqlServerFactAttribute"/>). Built from the model rather than by running migrations,
/// same as the Sqlite fixture — what these tests are about is transaction behaviour, not the
/// migration history.</para>
/// </summary>
public sealed class SqlServerCatalogTestContext : IDisposable
{
    private readonly string _databaseName = $"eTicketing_CatalogTests_{Guid.NewGuid():N}";

    public CatalogDbContext DbContext { get; }
    public IUserInteractionRepository UserInteractionRepository { get; }
    public IProductRepository ProductRepository { get; }
    public ICategoryRepository CategoryRepository { get; }
    public IUnitOfWork UnitOfWork => DbContext;

    public SqlServerCatalogTestContext()
    {
        DbContext = NewDbContext();
        DbContext.Database.EnsureCreated();

        UserInteractionRepository = new UserInteractionRepository(DbContext);
        ProductRepository = new ProductRepository(DbContext);
        CategoryRepository = new CategoryRepository(DbContext);
    }

    /// <summary>A context on its own connection — which is what makes a competing writer genuinely
    /// concurrent here, unlike the shared in-memory Sqlite connection the other fixture uses.</summary>
    public CatalogDbContext NewDbContext() =>
        new(new DbContextOptionsBuilder<CatalogDbContext>()
            .UseSqlServer(ConnectionString())
            .AddInterceptors(new AuditableEntitySaveChangesInterceptor())
            .Options);

    public PurchaseInteractionRecorder CreatePurchaseInteractionRecorder(IUnitOfWork? unitOfWork = null) =>
        new(UserInteractionRepository, ProductRepository, unitOfWork ?? UnitOfWork,
            NullLogger<PurchaseInteractionRecorder>.Instance);

    private string ConnectionString()
    {
        var builder = new Microsoft.Data.SqlClient.SqlConnectionStringBuilder(
            SqlServerFactAttribute.ConnectionString
            ?? throw new InvalidOperationException($"{SqlServerFactAttribute.ConnectionStringVariable} nije postavljen."))
        {
            InitialCatalog = _databaseName,
        };

        return builder.ConnectionString;
    }

    public void Dispose()
    {
        DbContext.Database.EnsureDeleted();
        DbContext.Dispose();
    }
}
