using eTicketing.Catalog.Data.Entities;
using eTicketing.Contracts.Persistence;

namespace eTicketing.Catalog.Data.Seeders;

/// <summary>
/// Dedicated seed-data file for Product, referenced by ProductConfiguration's HasData(). Ties into
/// the fixed-Guid organizations seeded in eTicketing.Identity.Data/Seeders/OrganizationSeeder.cs
/// (same Guids, different database/microservice — there's no FK across the boundary, just a
/// deliberate match) so a fresh deployment has real, cross-service-consistent data to exercise
/// the superadmin org-browsing product tab and category multiselect filter end-to-end. All seeded
/// rows are SingleOccurrence products (their Category is SingleOccurrence too), so Date is always set.
/// </summary>
public static class ProductSeeder
{
    private static readonly DateTime SeedTimestamp = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    // Must match eTicketing.Identity.Data.Seeders.OrganizationSeeder's fixed Ids.
    private static readonly Guid SarajevoEventsId = Guid.Parse("a1a1a1a1-0000-0000-0000-000000000001");
    private static readonly Guid MostarSportArenaId = Guid.Parse("a1a1a1a1-0000-0000-0000-000000000002");

    // City-center coordinates, used as a reasonable default pin for seed data only.
    private const double SarajevoLatitude = 43.8563;
    private const double SarajevoLongitude = 18.4131;
    private const double MostarLatitude = 43.3438;
    private const double MostarLongitude = 17.8078;

    public static Product[] GetSeedData() =>
    [
        new Product
        {
            Id = Guid.Parse("c3c3c3c3-0000-0000-0000-000000000001"),
            Name = "Ljetni Muzički Festival",
            Description = "Trodnevni festival na otvorenom sa regionalnim izvođačima.",
            Date = new DateTime(2026, 7, 15, 20, 0, 0, DateTimeKind.Utc),
            CategoryId = 1, // Muzika
            OrganizationId = SarajevoEventsId,
            Status = PublishStatus.Published,
            Latitude = SarajevoLatitude,
            Longitude = SarajevoLongitude,
            City = City.Sarajevo,
            CreatedAt = SeedTimestamp,
            UpdatedAt = SeedTimestamp,
        },
        new Product
        {
            Id = Guid.Parse("c3c3c3c3-0000-0000-0000-000000000002"),
            Name = "Akustična Večer u Vijećnici",
            Description = "Intimni akustični koncert u Vijećnici.",
            Date = new DateTime(2026, 9, 5, 19, 0, 0, DateTimeKind.Utc),
            CategoryId = 1, // Muzika
            OrganizationId = SarajevoEventsId,
            Status = PublishStatus.Published,
            Latitude = SarajevoLatitude,
            Longitude = SarajevoLongitude,
            City = City.Sarajevo,
            CreatedAt = SeedTimestamp,
            UpdatedAt = SeedTimestamp,
        },
        new Product
        {
            Id = Guid.Parse("c3c3c3c3-0000-0000-0000-000000000003"),
            Name = "Košarkaški Kup Mostar",
            Description = "Regionalni košarkaški turnir za klupske ekipe.",
            Date = new DateTime(2026, 8, 20, 18, 0, 0, DateTimeKind.Utc),
            CategoryId = 2, // Sport
            OrganizationId = MostarSportArenaId,
            Status = PublishStatus.Published,
            Latitude = MostarLatitude,
            Longitude = MostarLongitude,
            City = City.Mostar,
            CreatedAt = SeedTimestamp,
            UpdatedAt = SeedTimestamp,
        },
        new Product
        {
            Id = Guid.Parse("c3c3c3c3-0000-0000-0000-000000000004"),
            Name = "Maraton Mostar",
            Description = "Gradski maraton kroz historijsku jezgru Mostara.",
            Date = new DateTime(2026, 10, 10, 9, 0, 0, DateTimeKind.Utc),
            CategoryId = 2, // Sport
            OrganizationId = MostarSportArenaId,
            Status = PublishStatus.Published,
            Latitude = MostarLatitude,
            Longitude = MostarLongitude,
            City = City.Mostar,
            CreatedAt = SeedTimestamp,
            UpdatedAt = SeedTimestamp,
        },
        new Product
        {
            Id = Guid.Parse("c3c3c3c3-0000-0000-0000-000000000005"),
            Name = "Tech Konferencija Sarajevo",
            Description = "Konferencija o softverskom razvoju i startupima.",
            Date = new DateTime(2026, 11, 2, 9, 0, 0, DateTimeKind.Utc),
            CategoryId = 3, // Tehnologija
            OrganizationId = SarajevoEventsId,
            Status = PublishStatus.Published,
            Latitude = SarajevoLatitude,
            Longitude = SarajevoLongitude,
            City = City.Sarajevo,
            CreatedAt = SeedTimestamp,
            UpdatedAt = SeedTimestamp,
        },
        new Product
        {
            Id = Guid.Parse("c3c3c3c3-0000-0000-0000-000000000006"),
            Name = "Startup Meetup Mostar",
            Description = "Neformalno druženje lokalne startup zajednice.",
            Date = new DateTime(2026, 11, 20, 18, 0, 0, DateTimeKind.Utc),
            CategoryId = 3, // Tehnologija
            OrganizationId = MostarSportArenaId,
            Status = PublishStatus.Published,
            Latitude = MostarLatitude,
            Longitude = MostarLongitude,
            City = City.Mostar,
            CreatedAt = SeedTimestamp,
            UpdatedAt = SeedTimestamp,
        },
    ];
}
