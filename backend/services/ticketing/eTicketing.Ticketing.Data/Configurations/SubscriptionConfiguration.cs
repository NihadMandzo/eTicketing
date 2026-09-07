using eTicketing.Ticketing.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace eTicketing.Ticketing.Data.Configurations;

public class SubscriptionConfiguration : IEntityTypeConfiguration<Subscription>
{
    public void Configure(EntityTypeBuilder<Subscription> builder)
    {
        builder.Property(s => s.PaymentReference).HasMaxLength(200);
        builder.Property(s => s.UserEmail).HasMaxLength(320);
        builder.Property(s => s.CapacityHoldId).HasMaxLength(64);

        builder.HasOne(s => s.Sector)
            .WithMany()
            .HasForeignKey(s => s.SectorId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(s => s.UserId);
        builder.HasIndex(s => s.SectorId);

        // Every renewal, payment-failure and cancellation webhook resolves the subscription by this
        // reference, so it needs an index rather than a table scan. Unique because one provider
        // subscription is exactly one row here; filtered because the column is null until first
        // purchase completes, and SQL Server treats NULL as a value in a unique index.
        builder.HasIndex(s => s.PaymentReference)
            .IsUnique()
            .HasFilter("[PaymentReference] IS NOT NULL");
    }
}
