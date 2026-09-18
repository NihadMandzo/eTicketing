using eTicketing.Contracts.Pagination;
using eTicketing.Identity.Business.Shared.Validators;
using eTicketing.Identity.Data.Enums;
using Microsoft.AspNetCore.Http;

namespace eTicketing.Identity.Business.Organizations;

/// <summary>Plain mutable class, not a record — carries an IFormFile, bound via [FromForm].
/// Shared shape for both POST (create) and PUT (replace) /organizations/{id}/logo.</summary>
public class OrganizationLogoUploadRequest
{
    public IFormFile Logo { get; set; } = null!;
}
