using eTicketing.Catalog.Data.Entities;
using eTicketing.Catalog.Data.Seeders;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace eTicketing.Catalog.Data.Configurations;

public class EventConfiguration : IEntityTypeConfiguration<Event>
{
    public void Configure(EntityTypeBuilder<Event> builder)
    {
        builder.Property(e => e.Name).HasMaxLength(200).IsRequired();
        builder.Property(e => e.Description).HasMaxLength(2000);
        builder.Property(e => e.OrganizationId).IsRequired();

        // Restrict, not Cascade: deleting a category that still has events referencing it
        // should fail loudly rather than silently orphaning/deleting those events.
        builder.HasOne(e => e.Category)
            .WithMany(c => c.Events)
            .HasForeignKey(e => e.CategoryId)
            .OnDelete(DeleteBehavior.Restrict);

        // Hot filter paths for GET /events/all?OrganizationId=...&CategoryId=...
        builder.HasIndex(e => e.OrganizationId);
        builder.HasIndex(e => e.CategoryId);

        builder.HasData(EventSeeder.GetSeedData());
    }
}
