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

public class TicketPrintBatchFileConfiguration : IEntityTypeConfiguration<TicketPrintBatchFile>
{
    public void Configure(EntityTypeBuilder<TicketPrintBatchFile> builder)
    {
        builder.HasKey(f => f.BatchId);

        // Left to EF's default mapping, which is varbinary(max) on SQL Server — exactly what a
        // tens-of-megabytes sheet needs. Naming the type explicitly would hard-code SQL Server
        // syntax that the Sqlite in-memory database the tests run on cannot parse.
        builder.Property(f => f.Content).IsRequired();

        // The file cannot outlive its batch, and deleting the batch should take it along rather
        // than being blocked by a dependent row.
        builder.HasOne(f => f.Batch)
            .WithOne()
            .HasForeignKey<TicketPrintBatchFile>(f => f.BatchId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
