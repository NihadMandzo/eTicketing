using eTicketing.Catalog.Data.Repositories;
using eTicketing.Contracts.Pagination;
using eTicketing.Contracts.Results;
using Mapster;

namespace eTicketing.Catalog.Business.Events;

public class EventService : IEventService
{
    private readonly IEventRepository _eventRepository;

    public EventService(IEventRepository eventRepository)
    {
        _eventRepository = eventRepository;
    }

    public async Task<Result<PagedResult<EventResponse>>> GetAllAsync(EventQuery query, CancellationToken ct = default)
    {
        var paged = await _eventRepository.SearchAsync(query, query.OrganizationId, query.CategoryId, ct);
        return Result<PagedResult<EventResponse>>.Success(paged.Adapt<PagedResult<EventResponse>>());
    }

    public async Task<Result<List<Guid>>> GetOrganizationIdsAsync(IReadOnlyList<int> categoryIds, CancellationToken ct = default)
    {
        var ids = await _eventRepository.GetOrganizationIdsByCategoryIdsAsync(categoryIds, ct);
        return Result<List<Guid>>.Success(ids);
    }
}
