namespace eTicketing.Catalog.Business.Recommendations;

public sealed record RecommendationQuery
{
    public int Take { get; init; } = 8;
}
