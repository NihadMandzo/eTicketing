using eTicketing.Catalog.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace eTicketing.Catalog.Data.Configurations;

public class RecommendationModelSnapshotConfiguration : IEntityTypeConfiguration<RecommendationModelSnapshot>
{
    public void Configure(EntityTypeBuilder<RecommendationModelSnapshot> builder)
    {
        builder.Property(s => s.BlobName).HasMaxLength(200).IsRequired();

        // A plain index, not a filtered unique one. "Exactly one active snapshot" is enforced by
        // RecommendationModelSnapshotRepository.AddAndActivateAsync, which deactivates the previous
        // row in the same transaction as it inserts the new one — the only writer is the single
        // training path, so there is no race for a unique index to catch. The alternative,
        // HasFilter("[IsActive] = 1"), bakes provider-specific SQL into the model and would have to
        // be spelled differently for the Sqlite the test fixtures run on than for SQL Server.
        builder.HasIndex(s => s.IsActive);

        // The history table on the back-office screen reads newest-first.
        builder.HasIndex(s => s.TrainedAt);
    }
}
