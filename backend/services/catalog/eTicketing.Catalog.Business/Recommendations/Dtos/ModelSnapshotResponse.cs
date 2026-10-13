namespace eTicketing.Catalog.Business.Recommendations;

/// <summary>One past training run, newest first, for the back-office history table.</summary>
public record ModelSnapshotResponse(
    DateTime TrainedAt, int InteractionCount, int UserCount, int ProductCount, int TrainingDurationMs, bool IsActive);
