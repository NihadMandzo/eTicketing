using eTicketing.Identity.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace eTicketing.Identity.Data.Seeds;

public static class RoleSeed
{
    private static readonly DateTime SeedDate = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    public static void Seed(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Role>().HasData(
            new Role { Id = 1, Name = "SuperAdmin", Description = "Super Administrator sa punim pristupom sistemu", CreatedAt = SeedDate, UpdatedAt = SeedDate },
            new Role { Id = 2, Name = "Admin", Description = "Administrator sa pristupom cijeloj platformi", CreatedAt = SeedDate, UpdatedAt = SeedDate },
            new Role { Id = 3, Name = "OrganizationSuperAdmin", Description = "Super Administrator organizacije", CreatedAt = SeedDate, UpdatedAt = SeedDate },
            new Role { Id = 4, Name = "OrganizationAdmin", Description = "Administrator organizacije", CreatedAt = SeedDate, UpdatedAt = SeedDate },
            new Role { Id = 5, Name = "User", Description = "Registrovani korisnik (kupac)", CreatedAt = SeedDate, UpdatedAt = SeedDate }
        );
    }
}
