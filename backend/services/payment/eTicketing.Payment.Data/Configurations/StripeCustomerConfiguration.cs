using eTicketing.Payment.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace eTicketing.Payment.Data.Configurations;

public class StripeCustomerConfiguration : IEntityTypeConfiguration<StripeCustomer>
{
    public void Configure(EntityTypeBuilder<StripeCustomer> builder)
    {
        builder.Property(c => c.ProviderCustomerId).HasMaxLength(255).IsRequired();
        builder.Property(c => c.Email).HasMaxLength(320).IsRequired();

        // One Stripe customer per buyer: the whole point of the table.
        builder.HasIndex(c => c.UserId).IsUnique();
        builder.HasIndex(c => c.ProviderCustomerId).IsUnique();
    }
}
