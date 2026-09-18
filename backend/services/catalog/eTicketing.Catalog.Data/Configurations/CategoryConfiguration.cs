using eTicketing.Catalog.Data.Entities;
using eTicketing.Catalog.Data.Seeders;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace eTicketing.Catalog.Data.Configurations;

public class CategoryConfiguration : IEntityTypeConfiguration<Category>
{
    public void Configure(EntityTypeBuilder<Category> builder)
    {
        builder.Property(c => c.Name).HasMaxLength(100).IsRequired();
        builder.Property(c => c.Description).HasMaxLength(500);
        builder.Property(c => c.TicketingMode).IsRequired();
        builder.Property(c => c.IconBlobName).HasMaxLength(300);

        // The actual guarantee behind CategoryService's category.name_already_exists conflict:
        // the service check alone loses a race between two concurrent creates, which is precisely
        // what CatalogTestContext's RacingUnitOfWork exists to reproduce. Case sensitivity follows
        // the column's collation, so this is case-insensitive on SQL Server.
        builder.HasIndex(c => c.Name).IsUnique();

        builder.HasData(CategorySeeder.GetSeedData());
    }
}
