using eTicketing.Identity.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace eTicketing.Identity.Data.Configurations;

public class RefreshTokenConfiguration : IEntityTypeConfiguration<RefreshToken>
{
    public void Configure(EntityTypeBuilder<RefreshToken> builder)
    {
        builder.Property(t => t.TokenHash).HasMaxLength(200).IsRequired();
        builder.HasIndex(t => t.TokenHash).IsUnique();

        // Backs RefreshTokenRepository.TryRevokeAsync/RevokeAsync: those issue a single
        // conditional `UPDATE ... WHERE TokenHash = @hash AND RevokedAt IS NULL`. The unique
        // index on TokenHash means at most one row can ever match, and the database evaluates
        // that WHERE clause atomically as part of the UPDATE — so two concurrent conditional
        // updates against the same token hash can never both report a row affected. No separate
        // EF concurrency token (e.g. RowVersion) is needed for this guarantee; the unique index
        // plus the WHERE-clause condition on RevokedAt already give it.

        // Unlike User/Organization (Restrict), cascade here is correct — refresh tokens
        // are ephemeral session artifacts, not data worth protecting from a user's deletion.
        builder.HasOne(t => t.User)
            .WithMany()
            .HasForeignKey(t => t.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Ignore(t => t.IsActive);
    }
}
