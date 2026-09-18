using eTicketing.Contracts.Pagination;
using eTicketing.Contracts.Persistence;
using eTicketing.Identity.Data.Entities;

namespace eTicketing.Identity.Data.Repositories;

/// <summary>Projection, not an entity — one organization plus how many users belong to it, counted
/// in SQL. Lives here rather than being the Business-layer OrganizationResponse for the same reason
/// SearchAsync takes a BaseSearchObject: Data cannot reference Business.</summary>
public record OrganizationWithUserCount(Organization Organization, int UserCount);
