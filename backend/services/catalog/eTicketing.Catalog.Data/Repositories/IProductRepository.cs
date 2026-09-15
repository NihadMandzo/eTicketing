using eTicketing.Catalog.Data.Entities;
using eTicketing.Contracts.Pagination;
using eTicketing.Contracts.Persistence;

namespace eTicketing.Catalog.Data.Repositories;

public interface IProductRepository : IRepository<Product, Guid>
{
    /// <summary>Paged, FTS-filtered (name) search, optionally narrowed by organization, category,
    /// status and/or city — backs ProductService.GetPublishedAsync/GetMineAsync/GetAllAsync.
    /// organizationId/categoryId/status/city are passed as plain parameters rather than the
    /// Business-layer ProductQuery type so Data doesn't need to reference Business (would be
    /// circular — Business already references Data).</summary>
    Task<PagedResult<Product>> SearchAsync(
        BaseSearchObject query, Guid? organizationId, int? categoryId, PublishStatus? status, City? city, CancellationToken ct = default);

    /// <summary>Distinct OrganizationIds among products whose CategoryId is in <paramref name="categoryIds"/>,
    /// any status — backs the org-list category multiselect filter (GET /products/organization-ids).</summary>
    Task<List<Guid>> GetOrganizationIdsByCategoryIdsAsync(IReadOnlyList<int> categoryIds, CancellationToken ct = default);

    /// <summary>Whether any product (any status) still references this category — backs
    /// CategoryService.DeleteAsync's proactive FK-conflict check.</summary>
    Task<bool> ExistsForCategoryAsync(int categoryId, CancellationToken ct = default);

    /// <summary>Loads a product together with its Category and Images — Category resolves the
    /// owning TicketingMode (e.g. for Ticketing's internal ownership/mode lookup), Images backs
    /// ProductService.ToResponse's ImageUrls and the image upload/delete/product-delete paths.</summary>
    Task<Product?> GetByIdWithCategoryAsync(Guid id, CancellationToken ct = default);

    /// <summary>The read-only sibling of <see cref="GetByIdWithCategoryAsync"/>, for the three
    /// callers that only shape a response out of it: ProductService.GetByIdAsync,
    /// ProductService.GetInternalAsync and RecommendationService's anchor lookup.
    ///
    /// Two methods rather than AsNoTracking on the one, because the other five callers write
    /// through the instance they get back. UploadImageAsync is the sharp case: it adds a
    /// ProductImage and relies on EF relationship fix-up to append it to the loaded Images
    /// collection before mapping the response — untracked, the upload would answer with a product
    /// missing the image it just accepted.</summary>
    Task<Product?> GetByIdWithCategoryNoTrackingAsync(Guid id, CancellationToken ct = default);

    /// <summary>Batch form of <see cref="GetByIdWithCategoryAsync"/> without Images — backs the
    /// internal POST /internal/products/by-ids lookup eTicketing.Ticketing uses to label the
    /// organizer's gate-validation list. Images are deliberately not included: that caller only
    /// needs Name/Date/TicketingMode, and pulling up to 5 image rows per product for it would be
    /// pure waste.</summary>
    Task<List<Product>> GetByIdsWithCategoryAsync(IReadOnlyList<Guid> ids, CancellationToken ct = default);

    /// <summary>Published products by id, WITH Images — the recommendation surfaces render full
    /// product cards, so unlike <see cref="GetByIdsWithCategoryAsync"/> above they do need the
    /// image rows. Unknown, Draft, or deleted ids are simply absent from the result rather than an
    /// error: a recommendation list is built from interaction history that can outlive the product
    /// it points at.</summary>
    Task<List<Product>> GetPublishedByIdsWithDetailsAsync(IReadOnlyList<Guid> ids, CancellationToken ct = default);

    /// <summary>The candidate pool a recommendation is ranked out of: published products with
    /// Category and Images, newest first, hard-capped at <paramref name="take"/>. Scoring happens
    /// in memory afterwards (the model's predictions aren't expressible in SQL), so this cap is the
    /// only thing bounding that work.</summary>
    Task<List<Product>> GetPublishedCandidatesAsync(int take, CancellationToken ct = default);

    /// <summary>Published, SingleOccurrence products with a future Date, soonest first — backs the
    /// internal GET /internal/products/upcoming that eTicketing.Ticketing's Dashboard "Nadolazeći
    /// događaji" card calls. organizationId narrows to one organizer's own events; null means
    /// platform-wide, for the SuperAdmin/Admin dashboards. DailyEntry/RecurringReservation products
    /// have no single Date and are never candidates here.</summary>
    Task<List<Product>> GetUpcomingAsync(
        Guid? organizationId, DateTime nowUtc, int count, CancellationToken ct = default);

    /// <summary>One row per organization that owns at least one product, with the counts the
    /// Organizacije report needs — backs the internal GET /internal/products/organization-stats
    /// that eTicketing.Ticketing calls. Aggregated here rather than by loading every product and
    /// grouping in memory: this is a platform-wide question, and the answer is a handful of rows
    /// whatever the catalogue's size.</summary>
    Task<List<OrganizationProductStats>> GetOrganizationStatsAsync(CancellationToken ct = default);
}

/// <summary>Projection, not an entity — catalogue counts for one organization.
/// <paramref name="WithoutImage"/> counts products with no gallery photo at all, which is the
/// operational gap an Admin scans that column for.</summary>
public record OrganizationProductStats(
    Guid OrganizationId, int Total, int Published, int Draft, int WithoutImage);
