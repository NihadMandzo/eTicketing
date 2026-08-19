using eTicketing.Ticketing.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace eTicketing.Ticketing.Data.Configurations;

public class TicketConfiguration : IEntityTypeConfiguration<Ticket>
{
    public void Configure(EntityTypeBuilder<Ticket> builder)
    {
        builder.Property(t => t.UserEmail).HasMaxLength(320).IsRequired();
        builder.Property(t => t.PricePaid).HasColumnType("decimal(10,2)");
        builder.Property(t => t.PdfBlobName).HasMaxLength(300);

        builder.HasOne(t => t.Sector)
            .WithMany()
            .HasForeignKey(t => t.SectorId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(t => t.Subscription)
            .WithMany()
            .HasForeignKey(t => t.SubscriptionId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(t => t.UserId);
        builder.HasIndex(t => t.SectorId);
        builder.HasIndex(t => t.ProductId);
    }
}
