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
        // row in the same transaction as it inserts the new one. Within one Catalog instance there
        // is no race for a unique index to catch: every write comes from a training run, and those
        // are serialized by MatrixFactorizationModel's training semaphore. The alternative,
        // HasFilter("[IsActive] = 1"), bakes provider-specific SQL into the model and would have to
        // be spelled differently for the Sqlite the test fixtures run on than for SQL Server.
        //
        // Known limit of that choice: two Catalog instances retraining at the same instant could
        // each activate their own snapshot. The blast radius is one stale-but-active row, which
        // GetActiveAsync tolerates (FirstOrDefaultAsync) and the next retrain clears — accepted
        // deliberately rather than paying the provider-specific-SQL cost for it.
        builder.HasIndex(s => s.IsActive);

        // The history table on the back-office screen reads newest-first.
        builder.HasIndex(s => s.TrainedAt);
    }
}
