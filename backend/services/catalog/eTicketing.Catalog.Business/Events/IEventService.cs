using eTicketing.Contracts.Pagination;
using eTicketing.Contracts.Results;

namespace eTicketing.Catalog.Business.Events;

public interface IEventService
{
    Task<Result<PagedResult<EventResponse>>> GetAllAsync(EventQuery query, CancellationToken ct = default);

    /// <summary>Distinct organization ids among events in the given categories (any status) —
    /// backs the org-list category multiselect filter.</summary>
    Task<Result<List<Guid>>> GetOrganizationIdsAsync(IReadOnlyList<int> categoryIds, CancellationToken ct = default);
}
