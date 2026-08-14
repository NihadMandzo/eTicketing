using eTicketing.Catalog.Data.Entities;

namespace eTicketing.Catalog.Data.Seeders;

/// <summary>Dedicated seed-data file for Category, referenced by CategoryConfiguration's
/// HasData() — kept separate from the entity configuration itself so seed content can grow
/// (and be reviewed/diffed) independently of the mapping rules.</summary>
public static class CategorySeeder
{
    // HasData() seed rows bypass AuditableEntitySaveChangesInterceptor (it only fires for
    // SaveChanges calls, not migration-time INSERTs), so CreatedAt/UpdatedAt must be set
    // explicitly here or they'd land as 0001-01-01.
    private static readonly DateTime SeedTimestamp = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    /// <summary>IconBlobName is deliberately left null for every seeded category — images aren't
    /// seeded (per the seeding requirements), only uploaded later through the Category management
    /// screen's dedicated icon endpoints. IconUrl comes back null for these until then.</summary>
    public static Category[] GetSeedData() =>
    [
        new Category
        {
            Id = 1,
            Name = "Muzika",
            Description = "Koncerti i muzički festivali",
            IsActive = true,
            DisplayOrder = 1,
            IconBlobName = null,
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
            IconBlobName = null,
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
            IconBlobName = null,
            CreatedAt = SeedTimestamp,
            UpdatedAt = SeedTimestamp,
        },
    ];
}
