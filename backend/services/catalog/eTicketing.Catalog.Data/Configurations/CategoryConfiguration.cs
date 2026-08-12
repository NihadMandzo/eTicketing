using eTicketing.Catalog.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace eTicketing.Catalog.Data.Configurations;

public class CategoryConfiguration : IEntityTypeConfiguration<Category>
{
    // HasData() seed rows bypass AuditableEntitySaveChangesInterceptor (it only fires for
    // SaveChanges calls, not migration-time INSERTs), so CreatedAt/UpdatedAt must be set
    // explicitly here or they'd land as 0001-01-01.
    private static readonly DateTime SeedTimestamp = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    public void Configure(EntityTypeBuilder<Category> builder)
    {
        builder.Property(c => c.Name).HasMaxLength(100).IsRequired();
        builder.Property(c => c.Description).HasMaxLength(500);
        builder.Property(c => c.IconContentType).HasMaxLength(50);

        // Seeded rows get empty IconData — GET /categories/{id}/icon returns an empty-but-200
        // response for these until someone edits them with a real icon through the running
        // Category management screen. Cosmetic-only limitation, not a functional gap.
        builder.HasData(
            new Category
            {
                Id = 1,
                Name = "Muzika",
                Description = "Koncerti i muzički festivali",
                IsActive = true,
                DisplayOrder = 1,
                IconData = [],
                IconContentType = "image/png",
                CreatedAt = SeedTimestamp,
                UpdatedAt = SeedTimestamp,
            },
            new Category
            {
                Id = 2,
                Name = "Sport",
                Description = "Sportski događaji",
                IsActive = true,
                DisplayOrder = 2,
                IconData = [],
                IconContentType = "image/png",
                CreatedAt = SeedTimestamp,
                UpdatedAt = SeedTimestamp,
            },
            new Category
            {
                Id = 3,
                Name = "Tehnologija",
                Description = "Konferencije i meetupovi",
                IsActive = true,
                DisplayOrder = 3,
                IconData = [],
                IconContentType = "image/png",
                CreatedAt = SeedTimestamp,
                UpdatedAt = SeedTimestamp,
            });
    }
}
