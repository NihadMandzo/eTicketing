using eTicketing.Contracts.Pagination;
using eTicketing.Contracts.Persistence;
using Microsoft.AspNetCore.Http;

namespace eTicketing.Catalog.Business.Categories;

public record UpdateCategoryRequest
{
    public string Name { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public bool IsActive { get; init; } = true;
    public int DisplayOrder { get; init; }
    public TicketingMode TicketingMode { get; init; } = TicketingMode.SingleOccurrence;
}
