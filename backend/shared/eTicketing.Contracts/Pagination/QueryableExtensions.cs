using Microsoft.EntityFrameworkCore;

namespace eTicketing.Contracts.Pagination;

public static class QueryableExtensions
{
    public static async Task<PagedResult<T>> ToPagedResultAsync<T>(
        this IQueryable<T> query, int? page, int? pageSize, CancellationToken ct = default)
    {
        var totalCount = await query.CountAsync(ct);

        if (page.HasValue && pageSize.HasValue)
        {
            query = query.Skip(page.Value * pageSize.Value).Take(pageSize.Value);
        }

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
