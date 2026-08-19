using eTicketing.Contracts.Persistence;
using eTicketing.Ticketing.Business.External;
using eTicketing.Ticketing.Business.Sectors;
using eTicketing.Ticketing.Data;
using eTicketing.Ticketing.Data.Repositories;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Moq;

namespace eTicketing.Ticketing.Business.Tests.TestFixtures;

/// <summary>
/// A real EF Core Sqlite in-memory <see cref="TicketingDbContext"/> wired with the real
/// <see cref="SectorRepository"/> (not mocked), mirroring
/// eTicketing.Identity.Business.Tests/TestFixtures/IdentityTestContext.cs and
/// eTicketing.Catalog.Business.Tests/TestFixtures/CatalogTestContext.cs. The genuine external
/// systems — the HTTP call to eTicketing.Catalog and the Redis-backed capacity lock — are mocked
/// with Moq rather than exercised for real, per .claude/rules/10-backend.md.
/// </summary>
public sealed class TicketingTestContext : IDisposable
{
    private readonly SqliteConnection _connection;

    public TicketingDbContext DbContext { get; }
    public ISectorRepository SectorRepository { get; }
    public IUnitOfWork UnitOfWork { get; }
    public Mock<ICatalogClient> CatalogClient { get; } = new();
    public Mock<ISectorCapacityLock> CapacityLock { get; } = new();

    public TicketingTestContext()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        var options = new DbContextOptionsBuilder<TicketingDbContext>()
            .UseSqlite(_connection)
            .AddInterceptors(new AuditableEntitySaveChangesInterceptor())
            .Options;

        DbContext = new TicketingDbContext(options);
        DbContext.Database.EnsureCreated();

        SectorRepository = new SectorRepository(DbContext);
        UnitOfWork = DbContext;
    }

    public ISectorService CreateSectorService() =>
        new SectorService(SectorRepository, CatalogClient.Object, CapacityLock.Object, UnitOfWork);

    public void Dispose()
    {
        DbContext.Dispose();
        _connection.Dispose();
    }
}
