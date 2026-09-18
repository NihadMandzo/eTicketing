namespace eTicketing.Identity.Business.Organizations;

/// <summary>
/// The shape served to signed-out visitors by GET /organizations/{id}/public — the "Organizator"
/// card the storefront renders on a product page (web and mobile both show name, address,
/// description, and email/phone as mailto:/tel: links).
///
/// <para>Contact details are deliberately kept: these are the organization's own published
/// business details, not any individual staff member's. What is deliberately dropped is
/// everything that only means something inside the back office —
/// <c>UserCount</c> (how many staff accounts an organization has), <c>IsActive</c> and
/// <c>CreatedAt</c>. Those were previously readable by anyone, unauthenticated, because
/// <see cref="OrganizationResponse"/> was served on an AllowAnonymous route.</para>
/// </summary>
public record OrganizationPublicResponse(
    Guid Id,
    string Name,
    string Description,
    string Address,
    string PhoneNumber,
    string Email,
    string? Website,
    string? LogoUrl);
