using eTicketing.Contracts.Pagination;
using eTicketing.Contracts.Persistence;

namespace eTicketing.Catalog.Business.Events;

public record EventResponse(
    Guid Id,
    string Name,
    string Description,
    DateTime Date,
    int CategoryId,
    string CategoryName,
    Guid OrganizationId,
    PublishStatus Status,
    string? ImageUrl,
    DateTime CreatedAt);

public sealed record EventQuery : BaseSearchObject
{
    public Guid? OrganizationId { get; init; }
    public int? CategoryId { get; init; }
}
