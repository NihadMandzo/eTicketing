using eTicketing.Ticketing.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace eTicketing.Ticketing.Data.Configurations;

public class TicketConfiguration : IEntityTypeConfiguration<Ticket>
{
    public void Configure(EntityTypeBuilder<Ticket> builder)
    {
        // Optional, not required: a printed ticket has no buyer at all (see TicketOrigin).
        builder.Property(t => t.UserEmail).HasMaxLength(320);
        builder.Property(t => t.PricePaid).HasColumnType("decimal(10,2)");

        // SectorId is required (non-nullable), so plain Restrict is unambiguous here: the delete
        // is always rejected by the database, there's no nullable client-side fallback for EF to
        // reach for.
        builder.HasOne(t => t.Sector)
            .WithMany()
            .HasForeignKey(t => t.SectorId)
            .OnDelete(DeleteBehavior.Restrict);

        // SubscriptionId/TicketTypeId are OPTIONAL (nullable) FKs. Plain Restrict on an optional FK
        // does NOT block the delete the way it does for a required FK — EF Core's client-side
        // fixup for a tracked dependent still nulls the FK out and lets the delete through
        // silently (functionally identical to ClientSetNull), even though Restrict was configured.
        // ClientNoAction disables that fixup entirely, so a delete that would orphan a Ticket is
        // always rejected by the database's own FK constraint (same DDL either way — no ON DELETE
        // clause, SQLite/SQL Server both default that to NO ACTION/restrict).
        builder.HasOne(t => t.Subscription)
            .WithMany()
            .HasForeignKey(t => t.SubscriptionId)
            .OnDelete(DeleteBehavior.ClientNoAction);

        builder.HasOne(t => t.TicketType)
            .WithMany()
            .HasForeignKey(t => t.TicketTypeId)
            .OnDelete(DeleteBehavior.ClientNoAction);

        // Cascade, unlike every FK above: a print batch owns its tickets outright — nobody bought
        // them and nothing else references them — so deleting the batch should take them with it
        // rather than being blocked. Nothing deletes batches today; this keeps that door open
        // without leaving orphans behind if it ever does.
        builder.HasOne(t => t.PrintBatch)
            .WithMany()
            .HasForeignKey(t => t.PrintBatchId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(t => t.UserId);
        builder.HasIndex(t => t.SectorId);
        builder.HasIndex(t => t.ProductId);
        builder.HasIndex(t => t.OrderId);
        builder.HasIndex(t => t.PrintBatchId);

        // The render worker pages through one batch in serial order, and TicketPrintService reads
        // MAX(SerialNumber) per product to continue numbering across batches. Unique so that two
        // overlapping CreateAsync calls for the same product — both racing past the in-flight check
        // and computing the same MAX(SerialNumber)+1 before either commits — cannot both mint
        // colliding stub numbers; the loser's SaveChangesAsync throws instead. SerialNumber is
        // nullable and every non-printed ticket leaves it null, which a unique index does not
        // constrain (NULLs are never considered equal to each other).
        builder.HasIndex(t => new { t.ProductId, t.SerialNumber }).IsUnique();
    }
}
