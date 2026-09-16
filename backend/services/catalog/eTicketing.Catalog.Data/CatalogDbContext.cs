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

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(CatalogDbContext).Assembly);

        // Lives in eTicketing.Contracts, so the assembly scan above does not reach it. Applied
        // explicitly here rather than copied, so all three services' outbox tables stay identical.
        modelBuilder.ApplyConfiguration(new OutboxMessageConfiguration());
    }
}
