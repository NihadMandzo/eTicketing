using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

// Inside namespace eTicketing.Payment.*, the unqualified identifier `Payment` binds to the
// namespace rather than the entity (CS0118), so the entity always needs an alias.
using PaymentEntity = eTicketing.Payment.Data.Entities.Payment;

namespace eTicketing.Payment.Data.Configurations;

public class PaymentConfiguration : IEntityTypeConfiguration<PaymentEntity>
{
    public void Configure(EntityTypeBuilder<PaymentEntity> builder)
    {
        builder.Property(p => p.OrderRef).HasMaxLength(100).IsRequired();
        builder.Property(p => p.Amount).HasColumnType("decimal(10,2)");
        builder.Property(p => p.Currency).HasMaxLength(3).IsRequired();
        builder.Property(p => p.ProviderPaymentIntentId).HasMaxLength(255);
        builder.Property(p => p.ProviderSubscriptionId).HasMaxLength(255);
        builder.Property(p => p.FailureCode).HasMaxLength(100);

        // Unique, not just indexed: OrderRef is the idempotency key PaymentService uses to detect a
        // replayed request (e.g. Ticketing's Polly retry re-sending after a response was lost) -- a
        // duplicate row here would mean the same order got charged twice.
        builder.HasIndex(p => p.OrderRef).IsUnique();

        // Filtered so the many rows that never reach a provider intent (mock-mode failures, rows
        // abandoned before creation) do not all collide on NULL under SQL Server's treatment of
        // NULL as a value in a unique index.
        builder.HasIndex(p => p.ProviderPaymentIntentId)
            .IsUnique()
            .HasFilter("[ProviderPaymentIntentId] IS NOT NULL");

        // Not unique: a subscription legitimately has one payment row per billing period.
        builder.HasIndex(p => p.ProviderSubscriptionId);
    }
}
