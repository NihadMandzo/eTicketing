using eTicketing.Catalog.Business.Products;
using eTicketing.Contracts.Persistence;

namespace eTicketing.Catalog.Business.Recommendations;

public sealed record PopularProductsQuery
{
    public City? City { get; init; }
    public int Take { get; init; } = 8;
}
