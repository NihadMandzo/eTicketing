using eTicketing.Contracts.Persistence;
using Microsoft.EntityFrameworkCore;
using PaymentEntity = eTicketing.Payment.Data.Entities.Payment;

namespace eTicketing.Payment.Data;

public class PaymentDbContext : DbContext, IUnitOfWork
{
    public PaymentDbContext(DbContextOptions<PaymentDbContext> options) : base(options) { }

    public DbSet<PaymentEntity> Payments => Set<PaymentEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(PaymentDbContext).Assembly);
    }
}
