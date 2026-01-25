using eTicketing.Services.Database.Entities;
using Microsoft.EntityFrameworkCore;

namespace eTicketing.Services.Database.Seeds;

public static class CategorySeed
{
    public static void Seed(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Category>().HasData(
            new Category
            {
                Id = 1,
                Name = "Music",
                Description = "Music events and concerts",
                IconUrl = "/icons/music.svg",
                IsActive = true,
                DisplayOrder = 1,
                CreatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                UpdatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc)
            },
            new Category
            {
                Id = 2,
                Name = "Sports",
                Description = "Sports events and games",
                IconUrl = "/icons/sports.svg",
                IsActive = true,
                DisplayOrder = 2,
                CreatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                UpdatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc)
            },
            new Category
            {
                Id = 3,
                Name = "Theater",
                Description = "Theater and performing arts",
                IconUrl = "/icons/theater.svg",
                IsActive = true,
                DisplayOrder = 3,
                CreatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                UpdatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc)
            },
            new Category
            {
                Id = 4,
                Name = "Conference",
                Description = "Conferences and seminars",
                IconUrl = "/icons/conference.svg",
                IsActive = true,
                DisplayOrder = 4,
                CreatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                UpdatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc)
            },
            new Category
            {
                Id = 5,
                Name = "Arts & Culture",
                Description = "Art exhibitions and cultural events",
                IconUrl = "/icons/arts.svg",
                IsActive = true,
                DisplayOrder = 5,
                CreatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                UpdatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc)
            }
        );
    }
}
