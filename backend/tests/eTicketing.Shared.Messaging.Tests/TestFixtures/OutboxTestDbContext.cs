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
