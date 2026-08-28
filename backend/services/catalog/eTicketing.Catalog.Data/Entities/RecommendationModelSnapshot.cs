using eTicketing.Contracts.Persistence;

namespace eTicketing.Catalog.Data.Entities;

/// <summary>
/// One completed training run of the recommendation model. The model itself is a binary artifact
/// in Azure Blob Storage (see <see cref="BlobName"/>); this row is the record of it — what it was
/// trained on, when, and how long it took.
///
/// It exists for two reasons. First, without it "when did the model last learn something" is
/// unanswerable after a restart, because the process memory that knew is gone — this is what backs
/// the desktop back-office screen. Second, it is how a restarting service finds the model to load:
/// the single row with <see cref="IsActive"/> set names the blob to pull.
/// </summary>
public class RecommendationModelSnapshot : BaseEntity
{
    public Guid Id { get; set; }

    /// <summary>Key of the serialized ML.NET model inside the private "ml-models" container.</summary>
    public string BlobName { get; set; } = string.Empty;

    public DateTime TrainedAt { get; set; }

    /// <summary>Rows of UserInteraction the run consumed, and the distinct users/products among
    /// them. A user or product that appears in none of them is unknown to the model and will fall
    /// through to the content-based path at scoring time — surfacing these counts is what makes
    /// that visible instead of mysterious.</summary>
    public int InteractionCount { get; set; }
    public int UserCount { get; set; }
    public int ProductCount { get; set; }

    public int TrainingDurationMs { get; set; }

    /// <summary>Exactly one row carries this at a time — the model currently loaded and scoring.
    /// Enforced by a filtered unique index, not by convention.</summary>
    public bool IsActive { get; set; }
}
