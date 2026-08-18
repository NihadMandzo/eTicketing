using eTicketing.Ticketing.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace eTicketing.Ticketing.Data.Configurations;

public class SectorConfiguration : IEntityTypeConfiguration<Sector>
{
    public void Configure(EntityTypeBuilder<Sector> builder)
    {
        builder.Property(s => s.Name).HasMaxLength(200).IsRequired();
        builder.Property(s => s.Price).HasColumnType("decimal(10,2)");
        builder.Property(s => s.ProductId).IsRequired();

        // Hot filter paths for GET /sectors?productId= and GET /sectors/mine.
        builder.HasIndex(s => s.ProductId);
        builder.HasIndex(s => s.OrganizationId);

        // DailyEntry lookups: "does a Sector for this Product+month already exist" and the
        // preview/create validation that resolves PeriodYear/PeriodMonth.
        builder.HasIndex(s => new { s.ProductId, s.PeriodYear, s.PeriodMonth });
    }
}
