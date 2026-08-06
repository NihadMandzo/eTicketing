using eTicketing.Contracts.Persistence;
using eTicketing.Identity.Data.Entities;

namespace eTicketing.Identity.Data.Repositories;

public class OrganizationRepository : Repository<Organization, Guid>, IOrganizationRepository
{
    public OrganizationRepository(IdentityDbContext context) : base(context) { }
}
