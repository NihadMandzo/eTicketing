using eTicketing.Identity.Data.Entities;

namespace eTicketing.Identity.Data.Repositories;

public class OrganizationRepository : eTicketing.Contracts.Persistence.Repository<Organization>, IOrganizationRepository
{
    public OrganizationRepository(IdentityDbContext context) : base(context) { }
}
