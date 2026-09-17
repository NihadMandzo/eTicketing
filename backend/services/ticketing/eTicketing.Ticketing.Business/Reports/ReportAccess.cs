using System.Security.Claims;
using eTicketing.Contracts.Results;
using eTicketing.Contracts.Security;
using eTicketing.Ticketing.Business.Security;

namespace eTicketing.Ticketing.Business.Reports;

/// <summary>The outcome of the role/tab matrix check: either an <see cref="Error"/> to return,
/// or the organization the caller's data must be scoped to (null = whole platform).</summary>
internal readonly record struct ReportAccess(Error? Error, Guid? OrganizationId)
{
    public static ReportAccess Denied(Error error) => new(error, null);
    public static ReportAccess Platform() => new(null, null);
    public static ReportAccess Organization(Guid id) => new(null, id);
}
