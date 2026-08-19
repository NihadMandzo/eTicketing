using eTicketing.Contracts.Pagination;
using eTicketing.Contracts.Persistence;
using Microsoft.AspNetCore.Http;

namespace eTicketing.Catalog.Business.Categories;

/// <summary>IconUrl is null until an icon has been uploaded via the dedicated
/// POST/PUT /categories/{id}/icon endpoints — categories no longer carry icon bytes at all
/// (see Category.IconBlobName); it's derived from Azure Blob Storage, not a stored column.</summary>
public record CategoryResponse(
    int Id,
    string Name,
    string Description,
    string? IconUrl,
    bool IsActive,
    int DisplayOrder,
    TicketingMode TicketingMode,
    DateTime CreatedAt,
    DateTime? UpdatedAt);

public sealed record CategoryQuery : BaseSearchObject;

public record CreateCategoryRequest
{
    public string Name { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public bool IsActive { get; init; } = true;
    public int DisplayOrder { get; init; }
    public TicketingMode TicketingMode { get; init; } = TicketingMode.SingleOccurrence;
}

public record UpdateCategoryRequest
{
    public string Name { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public bool IsActive { get; init; } = true;
    public int DisplayOrder { get; init; }
    public TicketingMode TicketingMode { get; init; } = TicketingMode.SingleOccurrence;
}

/// <summary>Plain mutable class, not a record — carries an IFormFile, bound via [FromForm].
/// Shared shape for both POST (create) and PUT (replace) /categories/{id}/icon.</summary>
public class CategoryIconUploadRequest
{
    public IFormFile Icon { get; set; } = null!;
}
