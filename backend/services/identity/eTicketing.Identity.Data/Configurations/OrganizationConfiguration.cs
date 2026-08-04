using eTicketing.Identity.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace eTicketing.Identity.Data.Configurations;

public class OrganizationConfiguration : IEntityTypeConfiguration<Organization>
{
    public void Configure(EntityTypeBuilder<Organization> builder)
    {
        builder.Property(o => o.Name).HasMaxLength(200).IsRequired();
        builder.Property(o => o.Description).HasMaxLength(1000);
        builder.Property(o => o.Address).HasMaxLength(500);
        builder.Property(o => o.PhoneNumber).HasMaxLength(20);
        builder.Property(o => o.Email).HasMaxLength(255);
        builder.Property(o => o.Website).HasMaxLength(255);
    }
}
