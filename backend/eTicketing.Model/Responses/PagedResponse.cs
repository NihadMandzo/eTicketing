namespace eTicketing.Model.Responses;

public class PagedResponse<T>
{
    public List<T> Items { get; set; } = new();
    public int TotalCount { get; set; }
    public int? Page { get; set; }
    public int? PageSize { get; set; }
}
