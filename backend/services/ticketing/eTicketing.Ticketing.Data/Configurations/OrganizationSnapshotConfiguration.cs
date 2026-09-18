using eTicketing.Ticketing.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace eTicketing.Ticketing.Data.Configurations;

public class OrganizationSnapshotConfiguration : IEntityTypeConfiguration<OrganizationSnapshot>
{
    public void Configure(EntityTypeBuilder<OrganizationSnapshot> builder)
    {
        builder.HasKey(o => o.OrganizationId);
        builder.Property(o => o.OrganizationId).ValueGeneratedNever();

        // Same caps as the columns these mirror in eTicketing.Identity, so anything that fits there
        // fits here. Email/PhoneNumber/SuperAdminEmail are required at the source but are stored
        // nullable-tolerant lengths only — a projection must not be stricter than what it projects.
        builder.Property(o => o.Name).HasMaxLength(200).IsRequired();
        builder.Property(o => o.Address).HasMaxLength(500).IsRequired();
        builder.Property(o => o.Email).HasMaxLength(255).IsRequired();
        builder.Property(o => o.PhoneNumber).HasMaxLength(20).IsRequired();
        builder.Property(o => o.SuperAdminEmail).HasMaxLength(255);
    }
}
