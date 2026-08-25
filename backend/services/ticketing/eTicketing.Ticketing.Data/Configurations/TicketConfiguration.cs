using eTicketing.Ticketing.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace eTicketing.Ticketing.Data.Configurations;

public class TicketConfiguration : IEntityTypeConfiguration<Ticket>
{
    public void Configure(EntityTypeBuilder<Ticket> builder)
    {
        builder.Property(t => t.UserEmail).HasMaxLength(320).IsRequired();
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

        builder.HasIndex(t => t.UserId);
        builder.HasIndex(t => t.SectorId);
        builder.HasIndex(t => t.ProductId);
        builder.HasIndex(t => t.OrderId);
    }
}
