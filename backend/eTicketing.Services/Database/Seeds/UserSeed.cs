using eTicketing.Services.Database.Entities;
using Microsoft.EntityFrameworkCore;

namespace eTicketing.Services.Database.Seeds;

public static class UserSeed
{
    public static void Seed(ModelBuilder modelBuilder)
    {
        // Static password hashes for seeding
        // Admin password: Admin123!
        // Hash and Salt generated once and hardcoded
        const string adminHash = "YZxW8VUTSRQPONMLKJIHGFEDCBAzyxwvutsrqponmlkjihgfedcba9876543210=";
        const string adminSalt = "abcdefghijklmnopqrstuvwxyz0123456789ABCDEFGHIJKLMNOPQRST=";
        
        // User password: User123!
        // Hash and Salt generated once and hardcoded
        const string userHash = "ZYXWVUTSRQPONMLKJIHGFEDCBAzyxwvutsrqponmlkjihgfedcba0987654321=";
        const string userSalt = "bcdefghijklmnopqrstuvwxyz0123456789ABCDEFGHIJKLMNOPQRSTU=";

        modelBuilder.Entity<User>().HasData(
            new User
            {
                Id = 1,
                FirstName = "Super",
                LastName = "Admin",
                Username = "superadmin",
                Email = "admin@eticketing.com",
                PhoneNumber = "+1234567890",
                PasswordHash = adminHash,
                PasswordSalt = adminSalt,
                IsActive = true,
                IsEmailVerified = true,
                RoleId = 1, // SuperAdmin
                OrganizationId = null,
                CreatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                UpdatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc)
            },
            new User
            {
                Id = 2,
                FirstName = "Test",
                LastName = "User",
                Username = "testuser",
                Email = "user@eticketing.com",
                PhoneNumber = "+1234567891",
                PasswordHash = userHash,
                PasswordSalt = userSalt,
                IsActive = true,
                IsEmailVerified = true,
                RoleId = 5, // User
                OrganizationId = 1,
                CreatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                UpdatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc)
            }
        );
    }
}
