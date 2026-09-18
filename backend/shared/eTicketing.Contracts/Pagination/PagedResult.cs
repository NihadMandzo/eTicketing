namespace eTicketing.Contracts.Pagination;

/// <summary>
/// The locked pagination contract — see .claude/rules/01-domain.md and
/// docs/backend-projekt-template.md §6. Field names and 0-indexed paging (Page 0 is the first
/// page) are depended on by both the Angular and the Flutter Desktop PaginationBar, so neither
/// may change.
///
/// <para><c>Page</c>/<c>PageSize</c> are non-nullable on purpose. They were briefly
/// <c>int?</c>, and because <c>ToPagedResultAsync</c> then skipped paging entirely whenever
/// either was absent, a request that supplied only one of the two (which the web and mobile
/// catalog/sector services actually do) silently read the whole table.</para>
/// </summary>
public record PagedResult<T>
{
    public List<T> Items { get; init; } = [];
    public int TotalCount { get; init; }
    public int Page { get; init; }
    public int PageSize { get; init; }
}
