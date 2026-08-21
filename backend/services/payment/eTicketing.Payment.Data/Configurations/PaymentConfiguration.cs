using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PaymentEntity = eTicketing.Payment.Data.Entities.Payment;

namespace eTicketing.Payment.Data.Configurations;

// Note the PaymentEntity alias: within namespace eTicketing.Payment.*, an unqualified "Payment"
// binds to the eTicketing.Payment namespace itself rather than Entities.Payment (the root
// namespace segment collides with the entity's own name) — CS0118 without it.
public class PaymentConfiguration : IEntityTypeConfiguration<PaymentEntity>
{
    public void Configure(EntityTypeBuilder<PaymentEntity> builder)
    {
        builder.Property(p => p.OrderRef).HasMaxLength(100).IsRequired();
        builder.Property(p => p.Amount).HasColumnType("decimal(10,2)");

        // Unique, not just indexed: OrderRef is the idempotency key PaymentService.ChargeAsync
        // uses to detect a replayed charge request (e.g. Ticketing's Polly retry re-sending after
        // a response was lost) — a duplicate row here would mean the same order got charged twice.
        builder.HasIndex(p => p.OrderRef).IsUnique();
    }
}
