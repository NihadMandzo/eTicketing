using System.Diagnostics;
using eTicketing.Catalog.Data.Repositories;
using eTicketing.Shared.Storage;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.ML;
using Microsoft.ML.Data;
using Microsoft.ML.Trainers;

namespace eTicketing.Catalog.Business.Recommendations;

/// <summary>One row as ML.NET sees it. Guids travel as strings because MapValueToKey builds its
/// dictionary over a text column.</summary>
public sealed class InteractionRecord
{
    public string UserId { get; set; } = string.Empty;
    public string ProductId { get; set; } = string.Empty;
    public float Label { get; set; }
}

public sealed class InteractionPrediction
{
    public float Score { get; set; }
}

/// <summary>
/// ML.NET matrix factorization in one-class (implicit feedback) mode.
///
/// The domain has no ratings anywhere — a Ticket row is a pure positive signal, and there is no
/// such thing as a user telling us they disliked a product. That rules out ordinary regression MF,
/// whose loss only looks at cells you have values for, and calls for
/// <see cref="MatrixFactorizationTrainer.LossFunctionType.SquareLossOneClass"/>, which
/// additionally pushes every *unobserved* cell toward <c>C</c> weighted by <c>Alpha</c>. That
/// second term is the entire reason the model can rank a product a user has never touched.
///
/// Thread safety: the trained transformer and its prediction engine are swapped in atomically
/// behind a lock, and every read takes that lock. A retrain running concurrently with scoring is
/// therefore safe, and scoring keeps using the OLD model until the new one is completely ready.
/// Two retrains running concurrently are a separate problem — they would both write through the
/// same MLContext, which ML.NET does not document as safe — so training is additionally serialized
/// on its own semaphore. That is not hypothetical: the nightly job and the staff-triggered
/// POST /recommendations/retrain can fire at the same moment.
/// </summary>
public sealed class MatrixFactorizationModel : IRecommendationModel
{
    private const string UserColumn = "UserIdEncoded";
    private const string ProductColumn = "ProductIdEncoded";

    /// <summary>Weights above this are flattened. A user who opened the same product 200 times
    /// (a bot, a stuck refresh, a QA loop) is not 200 times more interested than one who opened it
    /// once, and without a cap that single cell would dominate the factorization.</summary>
    private const float MaxLabel = 10f;

    private readonly IBlobStorageService _blobStorage;
    private readonly RecommendationOptions _options;
    private readonly ILogger<MatrixFactorizationModel> _logger;

    private readonly Lock _gate = new();

    /// <summary>Serializes TrainAsync. Separate from <see cref="_gate"/> because a training run
    /// spans awaits (blob upload) and a plain lock cannot be held across those — and because
    /// holding the read lock for the whole run would block scoring, which the swap-at-the-end
    /// design exists precisely to avoid.</summary>
    private readonly SemaphoreSlim _trainingGate = new(1, 1);

    private readonly MLContext _mlContext = new(seed: 0);

    private PredictionEngine<InteractionRecord, InteractionPrediction>? _engine;
    private HashSet<Guid> _knownUsers = [];
    private HashSet<Guid> _knownProducts = [];

    public MatrixFactorizationModel(
        IBlobStorageService blobStorage,
        IOptions<RecommendationOptions> options,
        ILogger<MatrixFactorizationModel> logger)
    {
        _blobStorage = blobStorage;
        _options = options.Value;
        _logger = logger;
    }

    public bool IsTrained
    {
        get { lock (_gate) return _engine is not null; }
    }

    public async Task<TrainingOutcome> TrainAsync(IReadOnlyList<InteractionTrainingRow> rows, CancellationToken ct = default)
    {
        // A manual "Ponovo treniraj" landing inside the nightly window waits for it instead of
        // fitting a second pipeline through the same MLContext at the same time.
        await _trainingGate.WaitAsync(ct);
        try
        {
            return await TrainCoreAsync(rows, ct);
        }
        finally
        {
            _trainingGate.Release();
        }
    }

    private async Task<TrainingOutcome> TrainCoreAsync(IReadOnlyList<InteractionTrainingRow> rows, CancellationToken ct)
    {
        var stopwatch = Stopwatch.StartNew();

        var records = rows
            .Select(r => new InteractionRecord
            {
                UserId = r.UserId.ToString(),
                ProductId = r.ProductId.ToString(),
                Label = Math.Min(r.Weight, MaxLabel),
            })
            .ToList();

        var data = _mlContext.Data.LoadFromEnumerable(records);

        var pipeline = _mlContext.Transforms.Conversion
            .MapValueToKey(UserColumn, nameof(InteractionRecord.UserId))
            .Append(_mlContext.Transforms.Conversion.MapValueToKey(ProductColumn, nameof(InteractionRecord.ProductId)))
            .Append(_mlContext.Recommendation().Trainers.MatrixFactorization(new MatrixFactorizationTrainer.Options
            {
                MatrixColumnIndexColumnName = UserColumn,
                MatrixRowIndexColumnName = ProductColumn,
                LabelColumnName = nameof(InteractionRecord.Label),
                LossFunction = MatrixFactorizationTrainer.LossFunctionType.SquareLossOneClass,
                // Alpha weights the unobserved cells against the observed ones, and C is the value
                // they're pulled toward. Low values of both say "an absent interaction is weak
                // evidence of disinterest" — right for a catalog where a user has plausibly just
                // never seen most products.
                Alpha = 0.01,
                C = 0.00001,
                Lambda = 0.025,
                NumberOfIterations = 20,
                ApproximationRank = 64,
                Quiet = true,
            }));

        // Training is CPU-bound and synchronous inside ML.NET; pushed off the calling thread so the
        // manual "Ponovo treniraj" request and the nightly timer don't block a request thread.
        var transformer = await Task.Run(() => pipeline.Fit(data), ct);

        var knownUsers = rows.Select(r => r.UserId).ToHashSet();
        var knownProducts = rows.Select(r => r.ProductId).ToHashSet();

        var blobName = $"recommendation-model-{DateTime.UtcNow:yyyyMMddHHmmss}.zip";
        using (var buffer = new MemoryStream())
        {
            _mlContext.Model.Save(transformer, data.Schema, buffer);
            buffer.Position = 0;
            await _blobStorage.UploadPrivateAsync(_options.ModelContainer, blobName, buffer, "application/zip", ct);
        }

        // Swapped in only now — everything above could have thrown, and a failed retrain must leave
        // the previously trained model serving rather than knock the service down to no model.
        Swap(transformer, knownUsers, knownProducts);

        stopwatch.Stop();
        _logger.LogInformation(
            "Model preporuka istreniran: {Interactions} interakcija, {Users} korisnika, {Products} proizvoda, {Duration}ms → {BlobName}.",
            rows.Count, knownUsers.Count, knownProducts.Count, stopwatch.ElapsedMilliseconds, blobName);

        return new TrainingOutcome(blobName, rows.Count, knownUsers.Count, knownProducts.Count, (int)stopwatch.ElapsedMilliseconds);
    }

    public async Task<bool> LoadAsync(string blobName, CancellationToken ct = default)
    {
        await using var stream = await _blobStorage.DownloadAsync(_options.ModelContainer, blobName, ct);
        if (stream is null)
        {
            // The snapshot row outlived its artifact — someone cleared the container, or the
            // account changed. Recoverable by retraining, so it's a warning and a false, not a throw.
            _logger.LogWarning("Snimljeni model '{BlobName}' nije pronađen u pohrani — potrebno je ponovno treniranje.", blobName);
            return false;
        }

        var transformer = _mlContext.Model.Load(stream, out var inputSchema);
        var outputSchema = transformer.GetOutputSchema(inputSchema);

        Swap(transformer, ReadKnownIds(outputSchema, UserColumn), ReadKnownIds(outputSchema, ProductColumn));

        _logger.LogInformation(
            "Model preporuka učitan iz pohrane ({BlobName}): poznato {Users} korisnika i {Products} proizvoda.",
            blobName, _knownUsers.Count, _knownProducts.Count);
        return true;
    }

    public bool TryScore(Guid userId, Guid productId, out float score)
    {
        score = 0f;

        lock (_gate)
        {
            // Both checks matter and neither is redundant. MapValueToKey maps an unseen value to
            // the missing key rather than failing, so scoring an unknown user would quietly return
            // a meaningless number that the caller would then rank on.
            if (_engine is null || !_knownUsers.Contains(userId) || !_knownProducts.Contains(productId))
                return false;

            var prediction = _engine.Predict(new InteractionRecord
            {
                UserId = userId.ToString(),
                ProductId = productId.ToString(),
            });

            // One-class MF can return NaN for a degenerate factorization (e.g. trained on a single
            // interaction). Treating that as "no opinion" keeps NaN out of the sort comparer, where
            // it would silently corrupt the ordering.
            if (float.IsNaN(prediction.Score) || float.IsInfinity(prediction.Score))
                return false;

            score = prediction.Score;
            return true;
        }
    }

    private void Swap(ITransformer transformer, HashSet<Guid> users, HashSet<Guid> products)
    {
        var engine = _mlContext.Model.CreatePredictionEngine<InteractionRecord, InteractionPrediction>(transformer);

        lock (_gate)
        {
            // PredictionEngine is explicitly not thread-safe, which is why every Predict call above
            // runs under this same lock rather than the engine being handed out.
            _engine?.Dispose();
            _engine = engine;
            _knownUsers = users;
            _knownProducts = products;
        }
    }

    /// <summary>Recovers the ids the model was trained on from the key-value annotations
    /// MapValueToKey persisted into the saved model. This is what makes the blob self-sufficient:
    /// a restarted container can tell known ids from unknown ones without consulting the database,
    /// whose contents may have moved on since the model was built.</summary>
    private static HashSet<Guid> ReadKnownIds(DataViewSchema schema, string columnName)
    {
        var column = schema.GetColumnOrNull(columnName);
        if (column is null)
            return [];

        VBuffer<ReadOnlyMemory<char>> keys = default;
        column.Value.GetKeyValues(ref keys);

        return keys.DenseValues()
            .Select(k => Guid.TryParse(k.ToString(), out var id) ? id : (Guid?)null)
            .Where(id => id.HasValue)
            .Select(id => id!.Value)
            .ToHashSet();
    }
}
