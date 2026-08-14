using eTicketing.Identity.Data.Entities;

namespace eTicketing.Identity.Data.Seeders;

/// <summary>
/// Dedicated seed-data file for Organization, referenced by OrganizationConfiguration's
/// HasData(). Fixed Guids are deliberately matched by
/// eTicketing.Catalog.Data.Seeders.EventSeeder in the sibling service (no FK across the
/// service/database boundary — just a deliberate id match) so a fresh deployment has
/// cross-service-consistent data: these organizations' event tabs/category filters have real
/// events to show without needing an event-creation endpoint yet.
/// </summary>
public static class OrganizationSeeder
{
    private static readonly DateTime SeedTimestamp = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    /// <summary>LogoBlobName is deliberately left null for every seeded organization — images
    /// aren't seeded, only uploaded later through the dedicated logo endpoints.</summary>
    public static Organization[] GetSeedData() =>
    [
        new Organization
        {
            Id = Guid.Parse("a1a1a1a1-0000-0000-0000-000000000001"),
            Name = "Sarajevo Events",
            Description = "Organizator koncerata i festivala u Sarajevu.",
            Address = "Ferhadija 12, Sarajevo",
            PhoneNumber = "+387 33 123 456",
            Email = "info@sarajevo-events.ba",
            Website = "https://sarajevo-events.ba",
            IsActive = true,
            LogoBlobName = null,
            CreatedAt = SeedTimestamp,
            UpdatedAt = SeedTimestamp,
        },
        new Organization
        {
            Id = Guid.Parse("a1a1a1a1-0000-0000-0000-000000000002"),
            Name = "Mostar Sport Arena",
            Description = "Organizacija sportskih događaja u Mostaru i regiji.",
            Address = "Kralja Tomislava 5, Mostar",
            PhoneNumber = "+387 36 987 654",
            Email = "kontakt@mostar-sport.ba",
            Website = null,
            IsActive = true,
            LogoBlobName = null,
            CreatedAt = SeedTimestamp,
            UpdatedAt = SeedTimestamp,
        },
    ];
}
