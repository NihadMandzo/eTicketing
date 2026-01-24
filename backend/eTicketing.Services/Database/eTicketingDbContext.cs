using eTicketing.Services.Database.Entities;
using Microsoft.EntityFrameworkCore;

namespace eTicketing.Services.Database;

public class eTicketingDbContext : DbContext
{
    public eTicketingDbContext(DbContextOptions<eTicketingDbContext> options) : base(options)
    {
    }

    public DbSet<User> Users { get; set; }
    public DbSet<Role> Roles { get; set; }
    public DbSet<Organization> Organizations { get; set; }
    public DbSet<Category> Categories { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // User Configuration
        modelBuilder.Entity<User>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.FirstName).IsRequired().HasMaxLength(100);
            entity.Property(e => e.LastName).IsRequired().HasMaxLength(100);
            entity.Property(e => e.Email).IsRequired().HasMaxLength(255);
            entity.Property(e => e.PhoneNumber).HasMaxLength(20);
            entity.Property(e => e.PasswordHash).IsRequired().HasMaxLength(500);
            entity.Property(e => e.PasswordSalt).IsRequired().HasMaxLength(500);

            entity.HasIndex(e => e.Email).IsUnique();

            entity.HasOne(e => e.Role)
                  .WithMany(r => r.Users)
                  .HasForeignKey(e => e.RoleId)
                  .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(e => e.Organization)
                  .WithMany(o => o.Users)
                  .HasForeignKey(e => e.OrganizationId)
                  .OnDelete(DeleteBehavior.SetNull);
        });

        // Role Configuration
        modelBuilder.Entity<Role>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).IsRequired().HasMaxLength(100);
            entity.Property(e => e.Description).HasMaxLength(500);

            entity.HasIndex(e => e.Name).IsUnique();
        });

        // Organization Configuration
        modelBuilder.Entity<Organization>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).IsRequired().HasMaxLength(200);
            entity.Property(e => e.Description).HasMaxLength(1000);
            entity.Property(e => e.Address).HasMaxLength(500);
            entity.Property(e => e.PhoneNumber).HasMaxLength(20);
            entity.Property(e => e.Email).HasMaxLength(255);
            entity.Property(e => e.Website).HasMaxLength(255);
            entity.Property(e => e.LogoUrl).HasMaxLength(500);
        });

        // Category Configuration
        modelBuilder.Entity<Category>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).IsRequired().HasMaxLength(100);
            entity.Property(e => e.Description).HasMaxLength(500);
            entity.Property(e => e.IconUrl).HasMaxLength(500);

            entity.HasIndex(e => e.Name).IsUnique();
        });

        // Seed Data
        SeedData(modelBuilder);
    }

    private void SeedData(ModelBuilder modelBuilder)
    {
        // Seed Roles
        modelBuilder.Entity<Role>().HasData(
            new Role { Id = 1, Name = "SuperAdmin", Description = "Super Administrator with full system access", CreatedAt = DateTime.UtcNow },
            new Role { Id = 2, Name = "Admin", Description = "Administrator with system-wide access", CreatedAt = DateTime.UtcNow },
            new Role { Id = 3, Name = "OrganizationSuperAdmin", Description = "Organization Super Administrator", CreatedAt = DateTime.UtcNow },
            new Role { Id = 4, Name = "OrganizationAdmin", Description = "Organization Administrator", CreatedAt = DateTime.UtcNow },
            new Role { Id = 5, Name = "User", Description = "Regular user", CreatedAt = DateTime.UtcNow }
        );

        // Seed Organizations
        modelBuilder.Entity<Organization>().HasData(
            new Organization
            {
                Id = 1,
                Name = "Default Organization",
                Description = "Default system organization",
                Address = "123 Main St",
                PhoneNumber = "+1234567890",
                Email = "info@defaultorg.com",
                Website = "https://defaultorg.com",
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            }
        );

        // Seed Categories
        modelBuilder.Entity<Category>().HasData(
            new Category { Id = 1, Name = "Music", Description = "Music events and concerts", IconUrl = "/icons/music.svg", IsActive = true, DisplayOrder = 1, CreatedAt = DateTime.UtcNow },
            new Category { Id = 2, Name = "Sports", Description = "Sports events and games", IconUrl = "/icons/sports.svg", IsActive = true, DisplayOrder = 2, CreatedAt = DateTime.UtcNow },
            new Category { Id = 3, Name = "Theater", Description = "Theater and performing arts", IconUrl = "/icons/theater.svg", IsActive = true, DisplayOrder = 3, CreatedAt = DateTime.UtcNow },
            new Category { Id = 4, Name = "Conference", Description = "Conferences and seminars", IconUrl = "/icons/conference.svg", IsActive = true, DisplayOrder = 4, CreatedAt = DateTime.UtcNow }
        );

        // Seed Default SuperAdmin User (password: Admin123!)
        // Password hash and salt would be generated by your auth service
        modelBuilder.Entity<User>().HasData(
            new User
            {
                Id = 1,
                FirstName = "Super",
                LastName = "Admin",
                Email = "admin@eticketing.com",
                PhoneNumber = "+1234567890",
                PasswordHash = "vx8ZPtLFkLPfJ8q7qBqRaQ==", // Placeholder - should be generated properly
                PasswordSalt = "3i7kJUv8rz5mN9pQ2wX7yA==", // Placeholder - should be generated properly
                IsActive = true,
                IsEmailVerified = true,
                RoleId = 1,
                OrganizationId = null,
                CreatedAt = DateTime.UtcNow
            }
        );
    }
}
