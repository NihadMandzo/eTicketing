using eTicketing.Catalog.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace eTicketing.Catalog.Data.Configurations;

public class ProductImageConfiguration : IEntityTypeConfiguration<ProductImage>
{
    public void Configure(EntityTypeBuilder<ProductImage> builder)
    {
        builder.Property(i => i.BlobName).HasMaxLength(300).IsRequired();

        // Cascade here (unlike Product->Category's Restrict) is deliberate and safe: ProductImage
        // rows have no meaning without their Product, and ProductService.DeleteAsync deletes the
        // matching blobs itself right after this row-level cascade commits (same DB-first-then-
        // blob ordering as Category/Organization delete) — see ProductService.DeleteAsync.
        builder.HasOne(i => i.Product)
            .WithMany(p => p.Images)
            .HasForeignKey(i => i.ProductId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(i => i.ProductId);
    }
}
