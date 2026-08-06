namespace eTicketing.Contracts.Pagination;

public record PagedResult<T>
{
    public List<T> Items { get; init; } = [];
    public int TotalCount { get; init; }
    public int? Page { get; init; }
    public int? PageSize { get; init; }
}
