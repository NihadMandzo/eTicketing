using eTicketing.Services.Database.Entities;
using Microsoft.EntityFrameworkCore;

namespace eTicketing.Services.Database.Seeds;

public static class OrganizationSeed
{
    public static void Seed(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Organization>().HasData(
            new Organization
            {
                Id = 1,
                Name = "Default Organization",
                Description = "Default system organization",
                Address = "123 Main St, City, Country",
                PhoneNumber = "+1234567890",
                Email = "info@defaultorg.com",
                Website = "https://defaultorg.com",
                LogoUrl = "/logos/default.png",
                IsActive = true,
                CreatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                UpdatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc)
            },
            new Organization
            {
                Id = 2,
                Name = "Demo Events Inc",
                Description = "Demo organization for testing",
                Address = "456 Event Ave, City, Country",
                PhoneNumber = "+9876543210",
                Email = "contact@demoevents.com",
                Website = "https://demoevents.com",
                LogoUrl = "/logos/demo.png",
                IsActive = true,
                CreatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                UpdatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc)
            }
        );
    }
}
