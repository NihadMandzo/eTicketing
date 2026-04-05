using eTicketing.Services.Database.Entities;
using eTicketing.Services.Database.Seeds;
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
    public DbSet<Event> Events { get; set; }
    public DbSet<EventImage> EventImages { get; set; }
    public DbSet<Image> Images { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // User Configuration
        modelBuilder.Entity<User>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.FirstName).IsRequired().HasMaxLength(100);
            entity.Property(e => e.LastName).IsRequired().HasMaxLength(100);
            entity.Property(e => e.Username).IsRequired().HasMaxLength(50);
            entity.Property(e => e.Email).IsRequired().HasMaxLength(255);
            entity.Property(e => e.PhoneNumber).HasMaxLength(20).IsRequired(false);
            entity.Property(e => e.PasswordHash).IsRequired().HasMaxLength(500);
            entity.Property(e => e.PasswordSalt).IsRequired().HasMaxLength(500);
            entity.Property(e => e.OTP).HasMaxLength(10);

            entity.HasIndex(e => e.Email).IsUnique();
            entity.HasIndex(e => e.Username).IsUnique();

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

        // Image Configuration
        modelBuilder.Entity<Image>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.ImageUrl).IsRequired().HasMaxLength(1000);
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

            entity.HasOne(e => e.Image)
                  .WithMany()
                  .HasForeignKey(e => e.ImageId)
                  .OnDelete(DeleteBehavior.SetNull);
        });

        // Category Configuration
        modelBuilder.Entity<Category>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).IsRequired().HasMaxLength(100);
            entity.Property(e => e.Description).HasMaxLength(500);

            entity.HasIndex(e => e.Name).IsUnique();

            entity.HasOne(e => e.Image)
                  .WithMany()
                  .HasForeignKey(e => e.ImageId)
                  .OnDelete(DeleteBehavior.SetNull);
        });

        // Event Configuration
        modelBuilder.Entity<Event>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Title).IsRequired().HasMaxLength(200);
            entity.Property(e => e.Description).IsRequired().HasMaxLength(2000);
            entity.Property(e => e.EventDateTime).IsRequired();
            entity.Property(e => e.Location).IsRequired().HasMaxLength(500);
            entity.Property(e => e.Latitude).IsRequired();
            entity.Property(e => e.Longitude).IsRequired();

            entity.HasOne(e => e.Organization)
                  .WithMany(o => o.Events)
                  .HasForeignKey(e => e.OrganizationId)
                  .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(e => e.Category)
                  .WithMany()
                  .HasForeignKey(e => e.CategoryId)
                  .OnDelete(DeleteBehavior.SetNull);

            entity.HasIndex(e => e.OrganizationId);
            entity.HasIndex(e => e.EventDateTime);
        });

        // EventImage Configuration
        modelBuilder.Entity<EventImage>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.IsPrimary).IsRequired();

            entity.HasOne(e => e.Image)
                  .WithMany(i => i.EventImages)
                  .HasForeignKey(e => e.ImageId)
                  .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(e => e.Event)
                  .WithMany(ev => ev.Images)
                  .HasForeignKey(e => e.EventId)
                  .OnDelete(DeleteBehavior.Cascade);

            entity.HasIndex(e => e.EventId);
        });

        // EventTicket Configuration
        // Note: OrganizationId is denormalized from Event primarily for authorization filtering performance,
        //       allowing quick filtering of tickets by organization without joining to Events.
        // Consistency is enforced at application level in EventTicketService
        modelBuilder.Entity<EventTicket>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.TicketType).IsRequired().HasMaxLength(100);
            entity.Property(e => e.Price).IsRequired().HasColumnType("decimal(18,2)");
            entity.Property(e => e.Currency).IsRequired().HasMaxLength(3);
            entity.Property(e => e.PriceType).IsRequired().HasMaxLength(50);
            entity.Property(e => e.TotalTickets).IsRequired();
            entity.Property(e => e.TicketsSold).IsRequired();
            entity.Property(e => e.Description).HasMaxLength(1000);

            entity.HasOne(e => e.Event)
                  .WithMany(ev => ev.EventTickets)
                  .HasForeignKey(e => e.EventId)
                  .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(e => e.Organization)
                  .WithMany(o => o.EventTickets)
                  .HasForeignKey(e => e.OrganizationId)
                  .OnDelete(DeleteBehavior.Restrict);

            entity.HasIndex(e => e.EventId);
            entity.HasIndex(e => e.OrganizationId);
            entity.HasIndex(e => e.IsActive);
        });

        // Image Configuration (Generic image for Organization, Category, Event)
        // Using Restrict/ClientCascade to avoid multiple cascade paths on SQL Server
        modelBuilder.Entity<Image>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.ImageUrl).IsRequired().HasMaxLength(1000);
            entity.Property(e => e.IsPrimary).IsRequired();
            entity.Property(e => e.EntityType).IsRequired().HasMaxLength(50);

            entity.HasOne(e => e.Event)
                  .WithMany()
                  .HasForeignKey(e => e.EventId)
                  .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(e => e.Organization)
                  .WithMany(o => o.Images)
                  .HasForeignKey(e => e.OrganizationId)
                  .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(e => e.Category)
                  .WithMany(c => c.Images)
                  .HasForeignKey(e => e.CategoryId)
                  .OnDelete(DeleteBehavior.Restrict);

            entity.HasIndex(e => e.EventId);
            entity.HasIndex(e => e.OrganizationId);
            entity.HasIndex(e => e.CategoryId);
            entity.HasIndex(e => e.EntityType);

            // Ensure exactly one foreign key is set based on EntityType
            entity.ToTable(t => t.HasCheckConstraint(
                "CK_Images_ExactlyOneEntityFk",
                "([EventId] IS NOT NULL AND [OrganizationId] IS NULL AND [CategoryId] IS NULL AND [EntityType] = 'Event') " +
                "OR ([EventId] IS NULL AND [OrganizationId] IS NOT NULL AND [CategoryId] IS NULL AND [EntityType] = 'Organization') " +
                "OR ([EventId] IS NULL AND [OrganizationId] IS NULL AND [CategoryId] IS NOT NULL AND [EntityType] = 'Category')"));
        });

        // Seed Data
        SeedData(modelBuilder);
    }

    private void SeedData(ModelBuilder modelBuilder)
    {
        // Seed all entities using separate seed files
        RoleSeed.Seed(modelBuilder);
        OrganizationSeed.Seed(modelBuilder);
        CategorySeed.Seed(modelBuilder);
        UserSeed.Seed(modelBuilder);
    }
}
