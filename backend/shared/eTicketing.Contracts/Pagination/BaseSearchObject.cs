namespace eTicketing.Contracts.Pagination;

public record BaseSearchObject
{
    public int? Page { get; init; } = 0;
    public int? PageSize { get; init; } = 10;
    public string? FTS { get; init; }
    public bool? IsActive { get; init; }
}
