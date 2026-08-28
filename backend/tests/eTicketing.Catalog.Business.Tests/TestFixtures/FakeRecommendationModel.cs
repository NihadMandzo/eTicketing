using eTicketing.Catalog.Business.Recommendations;
using eTicketing.Catalog.Data.Repositories;

namespace eTicketing.Catalog.Business.Tests.TestFixtures;

/// <summary>
/// A stand-in for the trained model, so RecommendationService's fallback chain can be tested for
/// what it is — branching logic — instead of being tested through a real factorization fitted to
/// four rows, whose output would be neither predictable nor meaningful.
///
/// A test says what the model knows by calling <see cref="SetScore"/>; anything not set is
/// "unknown", which is exactly the signal that makes the service fall through to the content-based
/// path. The real MatrixFactorizationModel gets its own tests.
/// </summary>
public sealed class FakeRecommendationModel : IRecommendationModel
{
    private readonly Dictionary<(Guid UserId, Guid ProductId), float> _scores = [];

    public bool IsTrained { get; set; }

    /// <summary>Training runs recorded rather than performed — lets a test assert that the retrain
    /// path reached the model without paying for a real fit.</summary>
    public List<IReadOnlyList<InteractionTrainingRow>> TrainingRuns { get; } = [];

    /// <summary>What LoadAsync should return. False models the snapshot row outliving its blob.</summary>
    public bool LoadSucceeds { get; set; } = true;
    public List<string> LoadedBlobNames { get; } = [];

    public void SetScore(Guid userId, Guid productId, float score)
    {
        _scores[(userId, productId)] = score;
        IsTrained = true;
    }

    public Task<TrainingOutcome> TrainAsync(IReadOnlyList<InteractionTrainingRow> rows, CancellationToken ct = default)
    {
        TrainingRuns.Add(rows);
        IsTrained = true;

        return Task.FromResult(new TrainingOutcome(
            BlobName: $"fake-model-{TrainingRuns.Count}.zip",
            InteractionCount: rows.Count,
            UserCount: rows.Select(r => r.UserId).Distinct().Count(),
            ProductCount: rows.Select(r => r.ProductId).Distinct().Count(),
            TrainingDurationMs: 1));
    }

    public Task<bool> LoadAsync(string blobName, CancellationToken ct = default)
    {
        LoadedBlobNames.Add(blobName);
        if (LoadSucceeds)
            IsTrained = true;

        return Task.FromResult(LoadSucceeds);
    }

    public bool TryScore(Guid userId, Guid productId, out float score)
        => _scores.TryGetValue((userId, productId), out score);
}
