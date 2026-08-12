using eTicketing.Contracts.Pagination;
using Microsoft.AspNetCore.Http;

namespace eTicketing.Catalog.Business.Categories;

public record CategoryResponse(
    int Id,
    string Name,
    string Description,
    string IconUrl,
    bool IsActive,
    int DisplayOrder,
    DateTime CreatedAt,
    DateTime? UpdatedAt);

/// <summary>Raw icon bytes + content-type, returned by CategoryService.GetIconAsync and
/// streamed as-is by GET /categories/{id}/icon.</summary>
public record CategoryIcon(byte[] Data, string ContentType);

public sealed record CategoryQuery : BaseSearchObject;

/// <summary>Plain mutable class, not a record — carries an IFormFile, bound via [FromForm].</summary>
public class CreateCategoryRequest
{
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public int DisplayOrder { get; set; }
    public IFormFile Icon { get; set; } = null!;
}

public class UpdateCategoryRequest
{
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public int DisplayOrder { get; set; }
    public IFormFile? Icon { get; set; }
}
