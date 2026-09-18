using eTicketing.Contracts.Pagination;
using eTicketing.Contracts.Persistence;
using Microsoft.AspNetCore.Http;

namespace eTicketing.Catalog.Business.Products;

public sealed record ProductQuery : BaseSearchObject
{
    public Guid? OrganizationId { get; init; }
    public int? CategoryId { get; init; }
    public PublishStatus? Status { get; init; }

    // Optional — narrows results to a single city (see Product.City), unlike CategoryId/Status
    // this filter is available on the public GET /products endpoint too.
    public City? City { get; init; }
}
