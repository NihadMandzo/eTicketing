using eTicketing.Contracts.Pagination;
using eTicketing.Contracts.Persistence;
using Microsoft.AspNetCore.Http;

namespace eTicketing.Catalog.Business.Products;

/// <summary>Same shape for create and update — OrganizationId is never taken from this request,
/// always from the caller's JWT claim (see ProductService.CreateAsync). Images are managed
/// exclusively through the dedicated POST/DELETE /products/{id}/images endpoints, never bundled
/// in here — same convention as Category's icon. Latitude/Longitude/City are nullable here (so
/// "not sent" is distinguishable from "0,0"/"first enum value") but required in practice — see
/// CreateProductRequestValidator: the organizer must place an exact pin at creation time.</summary>
public record UpsertProductRequest
{
    public string Name { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public DateTime? Date { get; init; }
    public int CategoryId { get; init; }
    public double? Latitude { get; init; }
    public double? Longitude { get; init; }
    public City? City { get; init; }
}
