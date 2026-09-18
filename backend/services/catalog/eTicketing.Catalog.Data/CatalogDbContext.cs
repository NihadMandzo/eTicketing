using eTicketing.Catalog.Data.Entities;
using eTicketing.Contracts.Messaging;
using eTicketing.Contracts.Persistence;
using Microsoft.EntityFrameworkCore;

namespace eTicketing.Catalog.Data;

public class CatalogDbContext : DbContext, IUnitOfWork
{
    public CatalogDbContext(DbContextOptions<CatalogDbContext> options) : base(options) { }

    public DbSet<Category> Categories => Set<Category>();
    public DbSet<Product> Products => Set<Product>();
    public DbSet<ProductImage> ProductImages => Set<ProductImage>();
    public DbSet<UserInteraction> UserInteractions => Set<UserInteraction>();
    public DbSet<RecommendationModelSnapshot> RecommendationModelSnapshots => Set<RecommendationModelSnapshot>();

    /// <summary>Events waiting to reach the broker, written in the same transaction as the data
    /// that produced them. Not a domain table — see eTicketing.Contracts.Messaging.OutboxMessage.</summary>
    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();

    /// <summary>Inbound messages already processed, so a redelivered purchase is not counted twice.
    /// Not a domain table — see eTicketing.Contracts.Messaging.InboxMessage.</summary>
    public DbSet<InboxMessage> InboxMessages => Set<InboxMessage>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(CatalogDbContext).Assembly);

        // Both live in eTicketing.Contracts, so the assembly scan above does not reach them. Applied
        // explicitly here rather than copied, so every service's outbox and inbox tables stay
        // identical.
        modelBuilder.ApplyConfiguration(new OutboxMessageConfiguration());
        modelBuilder.ApplyConfiguration(new InboxMessageConfiguration());
    }
}
