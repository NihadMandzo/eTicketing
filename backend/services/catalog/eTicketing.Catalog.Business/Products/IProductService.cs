using System.Security.Claims;
using eTicketing.Contracts.Pagination;
using eTicketing.Contracts.Results;

namespace eTicketing.Catalog.Business.Products;

public interface IProductService
{
    /// <summary>Stateless — validates the request without writing to the database, using the
    /// same Validate() rules as CreateAsync. Used to preview both a brand-new product and edits
    /// to an existing one.</summary>
    Task<Result<ProductPreviewResponse>> PreviewAsync(UpsertProductRequest request, ClaimsPrincipal user, CancellationToken ct = default);

    /// <summary>Creates as Status=Draft, OrganizationId taken from the caller's JWT claim (never
    /// from the request body).</summary>
    Task<Result<ProductResponse>> CreateAsync(UpsertProductRequest request, ClaimsPrincipal user, CancellationToken ct = default);

    /// <summary>Draft → Published. Ownership-checked (own organization or PlatformStaff).</summary>
    Task<Result<ProductResponse>> PublishAsync(Guid id, ClaimsPrincipal user, CancellationToken ct = default);

    /// <summary>Saves edits regardless of status — a Published product stays Published after an
    /// edit (does not revert to Draft). Ownership-checked.</summary>
    Task<Result<ProductResponse>> UpdateAsync(Guid id, UpsertProductRequest request, ClaimsPrincipal user, CancellationToken ct = default);

    /// <summary>Ownership-checked.</summary>
    Task<Result> DeleteAsync(Guid id, ClaimsPrincipal user, CancellationToken ct = default);

    /// <summary>Public — Status=Published only, any organization.</summary>
    Task<Result<PagedResult<ProductResponse>>> GetPublishedAsync(ProductQuery query, CancellationToken ct = default);

    /// <summary>Organizer — own organization's products, any status.</summary>
    Task<Result<PagedResult<ProductResponse>>> GetMineAsync(ProductQuery query, ClaimsPrincipal user, CancellationToken ct = default);

    /// <summary>PlatformStaff — every organization's products, any status.</summary>
    Task<Result<PagedResult<ProductResponse>>> GetAllAsync(ProductQuery query, CancellationToken ct = default);

    /// <summary>Distinct organization ids among products in the given categories (any status) —
    /// backs the org-list category multiselect filter.</summary>
    Task<Result<List<Guid>>> GetOrganizationIdsAsync(IReadOnlyList<int> categoryIds, CancellationToken ct = default);

    /// <summary>Internal-only, never routed through the Gateway — used by eTicketing.Ticketing to
    /// verify Sector-creation ownership and to read the product's Category.TicketingMode.</summary>
    Task<Result<ProductInternalResponse>> GetInternalAsync(Guid id, CancellationToken ct = default);
}
