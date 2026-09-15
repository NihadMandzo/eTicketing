using Microsoft.EntityFrameworkCore;

namespace eTicketing.Contracts.Pagination;

public static class QueryableExtensions
{
    /// <summary>
    /// Pages an <see cref="IQueryable{T}"/> at the database level, 0-indexed
    /// (<c>Skip(page * pageSize)</c>), exactly as docs/backend-projekt-template.md §6 specifies.
    /// </summary>
    /// <remarks>Both parameters are non-nullable, and Skip/Take are unconditional, deliberately.
    /// This method used to accept <c>int?</c> and skip paging altogether when either was null,
    /// which turned a caller that passed only a page size into a full-table read. Callers hand in
    /// <c>BaseSearchObject.EffectivePage</c>/<c>EffectivePageSize</c>, which apply the defaults.
    /// </remarks>
    public static async Task<PagedResult<T>> ToPagedResultAsync<T>(
        this IQueryable<T> query, int page, int pageSize, CancellationToken ct = default)
    {
        var totalCount = await query.CountAsync(ct);

        query = query.Skip(page * pageSize).Take(pageSize);

        var items = await query.ToListAsync(ct);

        return new PagedResult<T>
        {
            Items = items,
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize
        };
    }
}
