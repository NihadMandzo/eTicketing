using eTicketing.Catalog.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace eTicketing.Catalog.Data.Configurations;

public class UserInteractionConfiguration : IEntityTypeConfiguration<UserInteraction>
{
    public void Configure(EntityTypeBuilder<UserInteraction> builder)
    {
        builder.Property(i => i.UserId).IsRequired();
        builder.Property(i => i.Count).IsRequired();

        // Cascade, unlike ProductConfiguration's Restrict on Category: a deleted product's
        // interaction history is meaningless and must go with it. Nothing references a
        // UserInteraction, so there is nothing to orphan.
        builder.HasOne(i => i.Product)
            .WithMany()
            .HasForeignKey(i => i.ProductId)
            .OnDelete(DeleteBehavior.Cascade);

        // THE invariant of this table: one row per (user, product, type). Everything in
        // UserInteractionRepository.UpsertAsync depends on the database enforcing it rather than
        // trusting the read-then-write to have been raced-free.
        builder.HasIndex(i => new { i.UserId, i.ProductId, i.Type }).IsUnique();

        // Popularity ("how many people bought this") and co-occurrence ("who else touched this")
        // both scan by product first, then filter by type.
        builder.HasIndex(i => new { i.ProductId, i.Type });
    }
}
