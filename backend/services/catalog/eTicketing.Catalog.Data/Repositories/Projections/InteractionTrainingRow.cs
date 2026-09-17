namespace eTicketing.Catalog.Data.Repositories;

/// <summary>One row of training input: which user touched which product, and how strongly. This is
/// the entire feature set the matrix-factorization model sees — no category, no price, no date.
/// That is the point of collaborative filtering: the structure is learned from co-occurrence
/// alone, not from anything anyone declared about the products.</summary>
public record InteractionTrainingRow(Guid UserId, Guid ProductId, float Weight);
