using eTicketing.Contracts.Messaging;
using eTicketing.Contracts.Persistence;
using eTicketing.Identity.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace eTicketing.Identity.Data;

public class IdentityDbContext : DbContext, IUnitOfWork
{
    public IdentityDbContext(DbContextOptions<IdentityDbContext> options) : base(options) { }

    public DbSet<User> Users => Set<User>();
    public DbSet<Organization> Organizations => Set<Organization>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<PasswordResetToken> PasswordResetTokens => Set<PasswordResetToken>();

    /// <summary>Events waiting to reach the broker, written in the same transaction as the data
    /// that produced them. Not a domain table — see eTicketing.Contracts.Messaging.OutboxMessage.</summary>
    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(IdentityDbContext).Assembly);

        // Lives in eTicketing.Contracts, so the assembly scan above does not reach it. Applied
        // explicitly here rather than copied, so all three services' outbox tables stay identical.
        modelBuilder.ApplyConfiguration(new OutboxMessageConfiguration());

        modelBuilder.Entity<User>()
            .HasIndex(u => u.Email)
            .IsUnique();

        modelBuilder.Entity<User>()
            .HasIndex(u => u.Username)
            .IsUnique();

        // Role is no longer a seeded/DB-backed entity — it's the compile-time RoleType enum.
    }
}
