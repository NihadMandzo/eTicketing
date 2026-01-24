using eTicketing.Services.Database.Entities;
using eTicketing.Services.Helpers;
using Microsoft.EntityFrameworkCore;

namespace eTicketing.Services.Database.Seeds;

public static class UserSeed
{
    public static void Seed(ModelBuilder modelBuilder)
    {
        // Generate password hash for default users using PBKDF2
        // Password: Admin123!
        PasswordHelper.CreatePasswordHash("Admin123!", out string adminHash, out string adminSalt);
        
        // Password: User123!
        PasswordHelper.CreatePasswordHash("User123!", out string userHash, out string userSalt);

        modelBuilder.Entity<User>().HasData(
            new User
            {
                Id = 1,
                FirstName = "Super",
                LastName = "Admin",
                Email = "admin@eticketing.com",
                PhoneNumber = "+1234567890",
                PasswordHash = adminHash,
                PasswordSalt = adminSalt,
                IsActive = true,
                IsEmailVerified = true,
                RoleId = 1, // SuperAdmin
                OrganizationId = null,
                CreatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc)
            },
            new User
            {
                Id = 2,
                FirstName = "Test",
                LastName = "User",
                Email = "user@eticketing.com",
                PhoneNumber = "+1234567891",
                PasswordHash = userHash,
                PasswordSalt = userSalt,
                IsActive = true,
                IsEmailVerified = true,
                RoleId = 5, // User
                OrganizationId = 1,
                CreatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc)
            }
        );
    }
}
