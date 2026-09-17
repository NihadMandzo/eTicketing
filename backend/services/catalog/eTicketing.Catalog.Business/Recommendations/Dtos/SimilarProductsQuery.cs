namespace eTicketing.Catalog.Business.Recommendations;

public sealed record SimilarProductsQuery
{
    public int Take { get; init; } = 6;
}
