using eTicketing.Catalog.Business.Recommendations;
using eTicketing.Catalog.Business.Tests.TestFixtures;
using eTicketing.Catalog.Data.Repositories;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace eTicketing.Catalog.Business.Tests.Recommendations;

/// <summary>
/// Exercises the real ML.NET model rather than the fake: whether it actually learns a co-purchase
/// pattern, whether it survives a round trip through blob storage, and — most importantly — whether
/// it says "I don't know" instead of inventing a number for ids it has never seen. That last
/// distinction is what the whole fallback chain in RecommendationService hangs on.
///
/// The blob store is the in-memory FakeBlobStorageService, so the round trip is real serialization
/// through a real byte array, just not a real network call to Azure.
/// </summary>
public class MatrixFactorizationModelTests
{
    private readonly FakeBlobStorageService _blobStorage = new();
    private readonly RecommendationOptions _options = new();

    private readonly Guid _concert = Guid.NewGuid();
    private readonly Guid _theatre = Guid.NewGuid();
    private readonly Guid _parking = Guid.NewGuid();
    private readonly Guid _museum = Guid.NewGuid();

    [Fact]
    public void TryScore_BeforeTraining_ReturnsFalse()
    {
        var model = CreateModel();

        model.IsTrained.Should().BeFalse();
        model.TryScore(Guid.NewGuid(), _concert, out _).Should().BeFalse();
    }

    [Fact]
    public async Task TryScore_ForAnUnseenUser_ReturnsFalse()
    {
        var model = CreateModel();
        await model.TrainAsync(BuildTrainingData());

        model.IsTrained.Should().BeTrue();
        model.TryScore(Guid.NewGuid(), _concert, out _).Should()
            .BeFalse("a user absent from training has no representation — reporting a score would be fabricating one");
    }

    [Fact]
    public async Task TryScore_ForAnUnseenProduct_ReturnsFalse()
    {
        var model = CreateModel();
        var rows = BuildTrainingData();
        await model.TrainAsync(rows);

        model.TryScore(rows[0].UserId, Guid.NewGuid(), out _).Should().BeFalse();
    }

    [Fact]
    public async Task TrainAsync_WithCoPurchaseSignal_ScoresTheCoPurchasedProductAboveAnUnrelatedOne()
    {
        // Two disjoint audiences: one buys concerts and theatre, the other parking and museums.
        // A user who has only bought a concert should be pulled toward theatre, not toward parking.
        var rows = BuildTrainingData();
        var concertGoer = rows.First(r => r.ProductId == _concert).UserId;

        var model = CreateModel();
        await model.TrainAsync(rows);

        model.TryScore(concertGoer, _theatre, out var theatreScore).Should().BeTrue();
        model.TryScore(concertGoer, _parking, out var parkingScore).Should().BeTrue();

        theatreScore.Should().BeGreaterThan(parkingScore,
            "collaborative filtering's whole job is to transfer one audience's taste onto a member of it");
    }

    [Fact]
    public async Task TrainAsync_UploadsTheModelToThePrivateContainer()
    {
        var model = CreateModel();

        var outcome = await model.TrainAsync(BuildTrainingData());

        _blobStorage.Exists(_options.ModelContainer, outcome.BlobName).Should().BeTrue();
        outcome.InteractionCount.Should().Be(BuildTrainingData().Count);
        outcome.UserCount.Should().BeGreaterThan(0);
        outcome.ProductCount.Should().Be(4);
    }

    [Fact]
    public async Task LoadAsync_AfterTraining_RestoresScoringOnAFreshInstance()
    {
        // The restart test. A new MatrixFactorizationModel over the same blob store is what a
        // restarted container has: no process memory, only what was persisted.
        var rows = BuildTrainingData();
        var trained = CreateModel();
        var outcome = await trained.TrainAsync(rows);

        var concertGoer = rows.First(r => r.ProductId == _concert).UserId;
        trained.TryScore(concertGoer, _theatre, out var scoreBefore).Should().BeTrue();

        var restarted = CreateModel();
        restarted.IsTrained.Should().BeFalse();

        (await restarted.LoadAsync(outcome.BlobName)).Should().BeTrue();

        restarted.IsTrained.Should().BeTrue();
        restarted.TryScore(concertGoer, _theatre, out var scoreAfter).Should()
            .BeTrue("the ids the model was trained on are recovered from the saved model, not from the database");
        scoreAfter.Should().BeApproximately(scoreBefore, 0.0001f);
    }

    [Fact]
    public async Task LoadAsync_AfterTraining_StillReportsUnseenUsersAsUnknown()
    {
        var trained = CreateModel();
        var outcome = await trained.TrainAsync(BuildTrainingData());

        var restarted = CreateModel();
        await restarted.LoadAsync(outcome.BlobName);

        restarted.TryScore(Guid.NewGuid(), _concert, out _).Should().BeFalse();
    }

    [Fact]
    public async Task LoadAsync_ForAMissingBlob_ReturnsFalseInsteadOfThrowing()
    {
        var model = CreateModel();

        var loaded = await model.LoadAsync("nema-me.zip");

        loaded.Should().BeFalse();
        model.IsTrained.Should().BeFalse();
    }

    [Fact]
    public async Task TrainAsync_RunConcurrently_SerializesTheTrainingRuns()
    {
        // The nightly job and a staff-triggered "Ponovo treniraj" can fire at the same moment, and
        // both fit a pipeline through the same MLContext — which ML.NET does not document as safe.
        // The delay widens the window so an unguarded implementation would actually overlap here.
        _blobStorage.UploadDelay = TimeSpan.FromMilliseconds(50);
        var model = CreateModel();
        var rows = BuildTrainingData();

        await Task.WhenAll(model.TrainAsync(rows), model.TrainAsync(rows));

        _blobStorage.MaxConcurrentUploads.Should().Be(
            1, "a retrain landing inside another one must queue behind it, not race it");
        model.IsTrained.Should().BeTrue();
        model.TryScore(rows[0].UserId, _concert, out _).Should()
            .BeTrue("the model left standing after two overlapping runs must still be a usable one");
    }

    private MatrixFactorizationModel CreateModel() =>
        new(_blobStorage, Options.Create(_options), NullLogger<MatrixFactorizationModel>.Instance);

    /// <summary>Two audiences with no overlap, ten users each. Deliberately more than a handful of
    /// rows: matrix factorization on three interactions produces a degenerate model whose output
    /// would be noise, and a test asserting on noise is worse than no test.</summary>
    private List<InteractionTrainingRow> BuildTrainingData()
    {
        var rows = new List<InteractionTrainingRow>();

        for (var i = 0; i < 10; i++)
        {
            var eventGoer = DeterministicGuid(i);
            rows.Add(new InteractionTrainingRow(eventGoer, _concert, 5f));
            rows.Add(new InteractionTrainingRow(eventGoer, _theatre, 5f));

            var commuter = DeterministicGuid(100 + i);
            rows.Add(new InteractionTrainingRow(commuter, _parking, 5f));
            rows.Add(new InteractionTrainingRow(commuter, _museum, 5f));
        }

        return rows;
    }

    /// <summary>Stable ids so a failing run can be reproduced — Guid.NewGuid would make this test
    /// pass or fail on different data every time.</summary>
    private static Guid DeterministicGuid(int seed)
    {
        var bytes = new byte[16];
        BitConverter.GetBytes(seed).CopyTo(bytes, 0);
        return new Guid(bytes);
    }
}
