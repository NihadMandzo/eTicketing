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
