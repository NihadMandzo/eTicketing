namespace eTicketing.Catalog.Business.Recommendations;

/// <summary>What one completed training run produced, handed back so the caller can persist it as
/// a RecommendationModelSnapshot. The model itself has already been uploaded by the time this
/// returns — this is the metadata about it.</summary>
public record TrainingOutcome(string BlobName, int InteractionCount, int UserCount, int ProductCount, int TrainingDurationMs);
