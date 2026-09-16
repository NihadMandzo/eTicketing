using eTicketing.Contracts.Messaging;
using eTicketing.Contracts.Persistence;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace eTicketing.Shared.Messaging.Tests.TestFixtures;

/// <summary>
/// Sqlite in-memory, the same pattern as every service's test fixture (see
/// eTicketing.Identity.Business.Tests/TestFixtures/IdentityTestContext.cs) — a real database with
/// real constraints rather than a mocked DbSet, and the same audit interceptor so CreatedAt is
/// populated exactly as it is in production. That matters here: the dispatcher orders by it.
/// </summary>
public sealed class OutboxTestContext : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly ServiceProvider _provider;

    public OutboxTestDbContext DbContext { get; }

    /// <summary>A real scope factory over the same connection, so the dispatcher — which opens its
    /// own scope per pass, as it does in production — sees the rows this fixture writes.</summary>
    public IServiceScopeFactory ScopeFactory { get; }

    public OutboxTestContext()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        var services = new ServiceCollection();
        services.AddDbContext<OutboxTestDbContext>(options => options
            .UseSqlite(_connection)
            .AddInterceptors(new AuditableEntitySaveChangesInterceptor()));

        _provider = services.BuildServiceProvider();
        ScopeFactory = _provider.GetRequiredService<IServiceScopeFactory>();

        DbContext = new DbContextOptionsBuilder<OutboxTestDbContext>()
            .UseSqlite(_connection)
            .AddInterceptors(new AuditableEntitySaveChangesInterceptor())
            .Options is var options
            ? new OutboxTestDbContext(options)
            : throw new InvalidOperationException();

        DbContext.Database.EnsureCreated();
    }

    public void Dispose()
    {
        DbContext.Dispose();
        _provider.Dispose();
        _connection.Dispose();
    }
}
