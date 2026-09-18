using eTicketing.Contracts.Pagination;
using eTicketing.Identity.Business.Shared.Validators;
using eTicketing.Identity.Data.Enums;
using Microsoft.AspNetCore.Http;

namespace eTicketing.Identity.Business.Organizations;

/// <summary>Query for GET /organizations/{id}/users. Was previously a plain BaseSearchObject
/// (see GetUsers/GetUsersAsync/SearchByOrganizationAsync) — that meant FTS silently had no
/// effect (SearchByOrganizationAsync never read it) and there was no way to narrow by role, so
/// the admins/superadmins sub-lists on the organization detail screen couldn't be told apart.</summary>
public sealed record OrganizationUserQuery : BaseSearchObject
{
    public RoleType? Role { get; init; }
}
