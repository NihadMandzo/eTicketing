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
        // Above SQL Server's 4000-character nvarchar ceiling, so this maps to nvarchar(max) rather
        // than nvarchar(10000) — the length still binds as a validation rule, it just isn't a
        // column width any more. Kept in step with CreateProductRequestValidator.
        builder.Property(p => p.Description).HasMaxLength(10000);
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

        // The public storefront list: ProductRepository.SearchAsync filters Status and *always*
        // sorts CreatedAt DESC, so the sort column is the second key — a seek on Status then an
        // ordered range scan, instead of sorting every published product on every page request.
        // Column order matters here: (CreatedAt, Status) would not support the Status seek.
        builder.HasIndex(p => new { p.Status, p.CreatedAt });

        builder.HasData(ProductSeeder.GetSeedData());
    }
}
