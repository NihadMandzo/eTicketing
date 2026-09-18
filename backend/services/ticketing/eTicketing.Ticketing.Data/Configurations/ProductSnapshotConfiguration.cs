using eTicketing.Ticketing.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace eTicketing.Ticketing.Data.Configurations;

public class ProductSnapshotConfiguration : IEntityTypeConfiguration<ProductSnapshot>
{
    public void Configure(EntityTypeBuilder<ProductSnapshot> builder)
    {
        // ProductId is the key, not a surrogate id: the row IS the product, and one row per product
        // is what makes an upsert out of every redelivery.
        builder.HasKey(p => p.ProductId);
        builder.Property(p => p.ProductId).ValueGeneratedNever();

        // Same cap as Catalog's own Product.Name, so a name that fits there always fits here.
        builder.Property(p => p.Name).HasMaxLength(200).IsRequired();

        // The public sector listing joins this table and filters on Status; without the index that
        // EXISTS becomes a scan on every anonymous browse.
        builder.HasIndex(p => p.Status);
    }
}
