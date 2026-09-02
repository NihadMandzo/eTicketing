namespace eTicketing.Ticketing.Business.External;

/// <summary>Mirrors eTicketing.Identity.Business.Organizations.OrganizationInternalResponse's JSON
/// shape — duplicated rather than shared across the service boundary (Ticketing never references
/// Identity's assemblies, only its HTTP contract), same convention as
/// <see cref="CatalogProductResponse"/>.</summary>
public record IdentityOrganizationResponse(Guid Id, string Name, string Address, bool IsActive);

/// <summary>Mirrors Identity's <c>OrganizationContactResponse</c>. Separate from
/// <see cref="IdentityOrganizationResponse"/> on both sides of the wire for the same reason: a
/// report row has no business carrying an email address, and only the deleted-product notice
/// needs one.</summary>
public record IdentityOrganizationContactResponse(
    Guid Id, string Name, string Email, string PhoneNumber, string? SuperAdminEmail);

/// <summary>
/// HTTP client interface to eTicketing.Identity's internal-only endpoints (never routed through
/// the Gateway).
///
/// Used by the Organizacije report only. Every other org-scoped decision in Ticketing reads
/// <c>organizationId</c> straight off the caller's token, which is exactly why this client did not
/// exist before: reports are the first feature that needs the organization's *name*, not just its
/// id, and a name is Identity's to own.
///
/// Interface lives in .Business per .claude/rules/10-backend.md; the concrete HttpClient-backed
/// implementation lives in .Api/Infrastructure.
/// </summary>
public interface IIdentityClient
{
    /// <summary>Resolves names for the organizations a report has ticket rows for. Unknown ids are
    /// simply absent from the result, never an error — an organization deleted between the sale
    /// and the report should not fail the whole page.</summary>
    Task<IReadOnlyList<IdentityOrganizationResponse>> GetOrganizationsAsync(
        IReadOnlyList<Guid> organizationIds, CancellationToken ct = default);

    /// <summary>Contact details for one organization, so a buyer whose event was deleted can be
    /// told who to ask for a refund. Null when the organization is unknown to Identity — the
    /// caller still sends the cancellation, just without a contact block, because a buyer being
    /// told their event is off matters more than the address being on it.</summary>
    Task<IdentityOrganizationContactResponse?> GetOrganizationContactAsync(
        Guid organizationId, CancellationToken ct = default);
}
