using eTicketing.Contracts.Pagination;
using eTicketing.Contracts.Persistence;
using Microsoft.AspNetCore.Http;

namespace eTicketing.Catalog.Business.Categories;

/// <summary>Plain mutable class, not a record — carries an IFormFile, bound via [FromForm].
/// Shared shape for both POST (create) and PUT (replace) /categories/{id}/icon.</summary>
public class CategoryIconUploadRequest
{
    public IFormFile Icon { get; set; } = null!;
}
