using eTicketing.Contracts.Pagination;
using eTicketing.Contracts.Persistence;
using Microsoft.AspNetCore.Http;

namespace eTicketing.Catalog.Business.Products;

/// <summary>Returned by both POST /products/preview (stateless, no DB write) and as the shape
/// PreviewAsync/CreateAsync validate against — see ProductService.Validate(). Images are
/// deliberately absent: a previewed product has no Id yet to attach an uploaded image to (same
/// reasoning as Category, whose icon can only be uploaded once the category exists).</summary>
public record ProductPreviewResponse(
    string Name,
    string Description,
    DateTime? Date,
    int CategoryId,
    string CategoryName,
    TicketingMode TicketingMode,
    double Latitude,
    double Longitude,
    City City);
