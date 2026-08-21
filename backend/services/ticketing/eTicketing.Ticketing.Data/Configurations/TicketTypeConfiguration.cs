using eTicketing.Ticketing.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace eTicketing.Ticketing.Data.Configurations;

public class TicketTypeConfiguration : IEntityTypeConfiguration<TicketType>
{
    public void Configure(EntityTypeBuilder<TicketType> builder)
    {
        builder.Property(t => t.Name).HasMaxLength(100).IsRequired();
        builder.Property(t => t.Price).HasColumnType("decimal(10,2)");

        // Restrict — a Sector can't be deleted while it still has TicketTypes (TicketType.SectorId
        // is a required/non-nullable FK, so Restrict here is unambiguous: EF has no nullable
        // client-side fallback, the delete is always rejected by the database).
        builder.HasOne(t => t.Sector)
            .WithMany(s => s.TicketTypes)
            .HasForeignKey(t => t.SectorId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(t => t.SectorId);
    }
}
