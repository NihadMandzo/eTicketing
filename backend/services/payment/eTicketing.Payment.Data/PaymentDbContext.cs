using eTicketing.Contracts.Persistence;
using eTicketing.Payment.Data.Entities;
using Microsoft.EntityFrameworkCore;

using PaymentEntity = eTicketing.Payment.Data.Entities.Payment;

namespace eTicketing.Payment.Data;

public class PaymentDbContext : DbContext, IUnitOfWork
{
    public PaymentDbContext(DbContextOptions<PaymentDbContext> options) : base(options) { }

    public DbSet<PaymentEntity> Payments => Set<PaymentEntity>();

    /// <summary>Webhook de-duplication -- see StripeEvent.</summary>
    public DbSet<StripeEvent> StripeEvents => Set<StripeEvent>();

    public DbSet<StripeCustomer> StripeCustomers => Set<StripeCustomer>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(PaymentDbContext).Assembly);
    }
}
