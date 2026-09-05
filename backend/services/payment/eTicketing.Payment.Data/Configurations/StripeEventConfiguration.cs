using eTicketing.Payment.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace eTicketing.Payment.Data.Configurations;

public class StripeEventConfiguration : IEntityTypeConfiguration<StripeEvent>
{
    public void Configure(EntityTypeBuilder<StripeEvent> builder)
    {
        // Stripe's own event id is the key, so a redelivery collides on insert instead of needing a
        // read-then-write check that two concurrent deliveries could both pass.
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).HasMaxLength(255).ValueGeneratedNever();
        builder.Property(e => e.Type).HasMaxLength(100).IsRequired();
    }
}
