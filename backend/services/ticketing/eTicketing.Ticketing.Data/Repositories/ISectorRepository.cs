using eTicketing.Contracts.Pagination;
using eTicketing.Contracts.Persistence;
using eTicketing.Ticketing.Data.Entities;

namespace eTicketing.Ticketing.Data.Repositories;

public interface ISectorRepository : IRepository<Sector, Guid>
{
    /// <summary>Paged, optionally narrowed by product, organization and/or status — backs
    /// SectorService.GetPublishedAsync/GetMineAsync/GetAllAsync. Parameters are passed plainly
    /// rather than the Business-layer SectorQuery type so Data doesn't need to reference
    /// Business.</summary>
    Task<PagedResult<Sector>> SearchAsync(
        BaseSearchObject query, Guid? productId, Guid? organizationId, PublishStatus? status, CancellationToken ct = default);
}
