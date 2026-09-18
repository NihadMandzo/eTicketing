using eTicketing.Contracts.Pagination;
using eTicketing.Contracts.Persistence;
using Microsoft.AspNetCore.Http;

namespace eTicketing.Catalog.Business.Products;

/// <summary>Plain mutable class, not a record — carries an IFormFile, bound via [FromForm].
/// Shape for POST /products/{id}/images (there is no PUT/replace equivalent — a full product can
/// carry up to 5 images, so "replace" is just "delete one, upload another").</summary>
public class ProductImageUploadRequest
{
    public IFormFile Image { get; set; } = null!;
}
