using eTicketing.Ticketing.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace eTicketing.Ticketing.Data.Configurations;

public class GateDeviceSectorConfiguration : IEntityTypeConfiguration<GateDeviceSector>
{
    public void Configure(EntityTypeBuilder<GateDeviceSector> builder)
    {
        builder.HasIndex(s => new { s.GateDeviceId, s.SectorId }).IsUnique();

        // Restrict, not Cascade: deleting a sector that a live gate is scoped to should fail loudly
        // rather than silently shrinking what that gate admits. The organizer re-scopes the device
        // first, then deletes the sector.
        builder.HasOne(s => s.Sector!)
            .WithMany()
            .HasForeignKey(s => s.SectorId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
