using eTicketing.Contracts.Messaging;
using eTicketing.Contracts.Persistence;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace eTicketing.Shared.Messaging.Tests.TestFixtures;

/// <summary>
/// A minimal DbContext standing in for any of the three services that keep an outbox. It applies
/// the same <see cref="OutboxMessageConfiguration"/> they do, so these tests exercise the real
/// table shape rather than a convenient approximation, and carries one ordinary entity so the
/// atomicity tests have something to commit alongside a message.
/// </summary>
public class OutboxTestDbContext : DbContext, IUnitOfWork
{
    public OutboxTestDbContext(DbContextOptions<OutboxTestDbContext> options) : base(options) { }

    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();
    public DbSet<Widget> Widgets => Set<Widget>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfiguration(new OutboxMessageConfiguration());
        modelBuilder.Entity<Widget>().HasKey(w => w.Id);
    }
}

/// <summary>Stands in for whatever domain row a service is writing when it publishes. Exists only
/// so a test can assert that the message and the data commit together.</summary>
public class Widget : BaseEntity
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
}

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
