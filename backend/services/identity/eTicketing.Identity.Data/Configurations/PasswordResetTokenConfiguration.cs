using eTicketing.Identity.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace eTicketing.Identity.Data.Configurations;

public class PasswordResetTokenConfiguration : IEntityTypeConfiguration<PasswordResetToken>
{
    public void Configure(EntityTypeBuilder<PasswordResetToken> builder)
    {
        builder.Property(t => t.TokenHash).HasMaxLength(200).IsRequired();
        builder.HasIndex(t => t.TokenHash).IsUnique();

        // Same reasoning as RefreshTokenConfiguration: cascade is correct here, a reset token is
        // an ephemeral session artifact, not data worth protecting from a user's deletion.
        builder.HasOne(t => t.User)
            .WithMany()
            .HasForeignKey(t => t.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Ignore(t => t.IsValid);
    }
}
