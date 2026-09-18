using eTicketing.Contracts.Pagination;
using eTicketing.Identity.Business.Shared.Validators;
using eTicketing.Identity.Data.Enums;

namespace eTicketing.Identity.Business.Admins;

/// <summary>Despite the "Admin" name (kept for route/backwards compatibility — see
/// AdminEndpoints), this now spans every non-buyer role: SuperAdmin, Admin,
/// OrganizationSuperAdmin, OrganizationAdmin. RoleFilters narrows to any subset of those (the
/// desktop role filter is a multiselect); null or empty returns all of them — same "absent
/// array-typed query param binds to an empty array, not null, so both must mean 'no filter'"
/// convention as OrganizationQuery.OrganizationIds.</summary>
public sealed record AdminQuery : BaseSearchObject
{
    public RoleType[]? RoleFilters { get; init; }
}
