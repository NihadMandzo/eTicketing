using eTicketing.Contracts.Pagination;
using eTicketing.Identity.Business.Shared.Validators;
using eTicketing.Identity.Data.Enums;
using Microsoft.AspNetCore.Http;

namespace eTicketing.Identity.Business.Organizations;

public sealed record OrganizationQuery : BaseSearchObject
{
    /// <summary>Set only when the desktop app has pre-resolved organization ids from the
    /// category multiselect filter (via Catalog's GET /events/organization-ids) — Organizations
    /// and Events/Categories live in separate microservices/databases, so this filter can't be
    /// applied as a join here, only as an explicit id list supplied by the caller.
    /// Guid[] (not List&lt;Guid&gt;) is required — minimal APIs' query-string binder only
    /// special-cases arrays of parsable types for multi-value query params
    /// (?OrganizationIds=x&amp;OrganizationIds=y); List&lt;Guid&gt; has no TryParse and crashes
    /// the app at startup ("must have a valid TryParse method").</summary>
    public Guid[]? OrganizationIds { get; init; }
}
