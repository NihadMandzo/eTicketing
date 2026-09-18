namespace eTicketing.Identity.Business.Admins;

/// <summary>Body for DELETE /admins/{id}. Reason/RecipientEmail are only required when the
/// target is an OrganizationAdmin (enforced in AdminService.DeleteAsync, which is the only place
/// that knows the target's role) — always send this body, even empty, since the route no longer
/// accepts a bare DELETE.</summary>
public sealed record DeleteAdminRequest
{
    public string? Reason { get; init; }
    public string? RecipientEmail { get; init; }
}
