using eTicketing.Ticketing.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace eTicketing.Ticketing.Data.Configurations;

public class TicketPrintBatchConfiguration : IEntityTypeConfiguration<TicketPrintBatch>
{
    public void Configure(EntityTypeBuilder<TicketPrintBatch> builder)
    {
        builder.Property(b => b.ProductName).HasMaxLength(200).IsRequired();
        builder.Property(b => b.NominalValue).HasColumnType("decimal(12,2)");
        builder.Property(b => b.ErrorMessage).HasMaxLength(500);

        builder.HasIndex(b => b.ProductId);
        builder.HasIndex(b => b.OrganizationId);

        // The two hot lookups: the render worker draining unfinished work at startup, and the
        // desktop badge polling for batches that still have something to show.
        builder.HasIndex(b => b.Status);
    }
}
