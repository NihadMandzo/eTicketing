namespace eTicketing.Contracts.Pagination;

public class BaseSearchObject
{
    public int? Page { get; set; } = 0;
    public int? PageSize { get; set; } = 10;
    public string? FTS { get; set; }
}
