using eTicketing.Catalog.Data.Entities;
using eTicketing.Catalog.Data.Seeders;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace eTicketing.Catalog.Data.Configurations;

public class ProductConfiguration : IEntityTypeConfiguration<Product>
{
    public void Configure(EntityTypeBuilder<Product> builder)
    {
        builder.Property(p => p.Name).HasMaxLength(200).IsRequired();
        builder.Property(p => p.Description).HasMaxLength(2000);
        builder.Property(p => p.OrganizationId).IsRequired();

        // Restrict, not Cascade: deleting a category that still has products referencing it
        // should fail loudly rather than silently orphaning/deleting those products.
        builder.HasOne(p => p.Category)
            .WithMany(c => c.Products)
            .HasForeignKey(p => p.CategoryId)
            .OnDelete(DeleteBehavior.Restrict);

        // Hot filter paths for GET /products/all?OrganizationId=...&CategoryId=...&City=...
        builder.HasIndex(p => p.OrganizationId);
        builder.HasIndex(p => p.CategoryId);
        builder.HasIndex(p => p.City);

        builder.HasData(ProductSeeder.GetSeedData());
    }
}
