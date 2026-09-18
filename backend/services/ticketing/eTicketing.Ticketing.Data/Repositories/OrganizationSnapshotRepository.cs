using eTicketing.Contracts.Persistence;
using eTicketing.Ticketing.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace eTicketing.Ticketing.Data.Repositories;

public class OrganizationSnapshotRepository : Repository<OrganizationSnapshot, Guid>, IOrganizationSnapshotRepository
{
    public OrganizationSnapshotRepository(TicketingDbContext context) : base(context) { }

    public Task<OrganizationSnapshot?> GetByIdNoTrackingAsync(Guid organizationId, CancellationToken ct = default)
        => Query().AsNoTracking().FirstOrDefaultAsync(o => o.OrganizationId == organizationId, ct);

    public Task<List<OrganizationSnapshot>> GetByIdsNoTrackingAsync(
        IReadOnlyList<Guid> organizationIds, CancellationToken ct = default)
    {
        if (organizationIds.Count == 0)
            return Task.FromResult(new List<OrganizationSnapshot>());

        return Query().AsNoTracking().Where(o => organizationIds.Contains(o.OrganizationId)).ToListAsync(ct);
    }
}
