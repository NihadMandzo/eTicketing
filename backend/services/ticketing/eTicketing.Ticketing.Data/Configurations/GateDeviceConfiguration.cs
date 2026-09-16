using eTicketing.Ticketing.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace eTicketing.Ticketing.Data.Configurations;

public class GateDeviceConfiguration : IEntityTypeConfiguration<GateDevice>
{
    public void Configure(EntityTypeBuilder<GateDevice> builder)
    {
        builder.Property(d => d.Name).HasMaxLength(100).IsRequired();

        // Fixed 64 because it is always SHA-256 rendered as lowercase hex. Unique because the
        // authentication handler looks a device up BY this column on every single scan — a
        // duplicate would make that lookup ambiguous at the worst possible moment.
        builder.Property(d => d.KeyHash).HasMaxLength(64).IsFixedLength().IsRequired();
        builder.HasIndex(d => d.KeyHash).IsUnique();

        builder.Property(d => d.KeyPrefix).HasMaxLength(24).IsRequired();

        // Backs the back-office list, which is always scoped to one organization and usually
        // filtered to one product.
        builder.HasIndex(d => d.OrganizationId);
        builder.HasIndex(d => d.ProductId);

        builder.HasMany(d => d.Sectors)
            .WithOne(s => s.GateDevice!)
            .HasForeignKey(s => s.GateDeviceId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
