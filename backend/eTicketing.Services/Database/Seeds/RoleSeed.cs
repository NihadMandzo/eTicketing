using eTicketing.Services.Database.Entities;
using Microsoft.EntityFrameworkCore;

namespace eTicketing.Services.Database.Seeds;

public static class RoleSeed
{
    public static void Seed(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Role>().HasData(
            new Role
            {
                Id = 1,
                Name = "SuperAdmin",
                Description = "Super Administrator with full system access",
                CreatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc)
            },
            new Role
            {
                Id = 2,
                Name = "Admin",
                Description = "Administrator with system-wide access",
                CreatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc)
            },
            new Role
            {
                Id = 3,
                Name = "OrganizationSuperAdmin",
                Description = "Organization Super Administrator",
                CreatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc)
            },
            new Role
            {
                Id = 4,
                Name = "OrganizationAdmin",
                Description = "Organization Administrator",
                CreatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc)
            },
            new Role
            {
                Id = 5,
                Name = "User",
                Description = "Regular user",
                CreatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc)
            }
        );
    }
}
