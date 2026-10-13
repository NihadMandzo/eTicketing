using eTicketing.Catalog.Business.Tests.TestFixtures;
using eTicketing.Catalog.Data.Entities;
using FluentAssertions;

namespace eTicketing.Catalog.Business.Tests.Recommendations;

/// <summary>
/// The training-run record carries no audit timestamps: <see cref="RecommendationModelSnapshot.TrainedAt"/>
/// already says when the row was written, and every remaining column is either how a restart finds
/// the model (BlobName, IsActive) or shown on the desktop Preporuke screen.
/// </summary>
public class RecommendationModelSnapshotSchemaTests : IDisposable
{
    private readonly CatalogTestContext _fixture = new();

    [Fact]
    public void RecommendationModelSnapshot_Model_HasNoAuditColumns()
    {
        var columns = _fixture.DbContext.Model.FindEntityType(typeof(RecommendationModelSnapshot))!
            .GetProperties().Select(p => p.Name);

        columns.Should().BeEquivalentTo(
            nameof(RecommendationModelSnapshot.Id),
            nameof(RecommendationModelSnapshot.BlobName),
            nameof(RecommendationModelSnapshot.TrainedAt),
            nameof(RecommendationModelSnapshot.InteractionCount),
            nameof(RecommendationModelSnapshot.UserCount),
            nameof(RecommendationModelSnapshot.ProductCount),
            nameof(RecommendationModelSnapshot.TrainingDurationMs),
            nameof(RecommendationModelSnapshot.IsActive));
    }

    public void Dispose() => _fixture.Dispose();
}
