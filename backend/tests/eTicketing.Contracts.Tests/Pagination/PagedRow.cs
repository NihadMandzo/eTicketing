namespace eTicketing.Contracts.Tests.Pagination;

/// <summary>A throwaway entity, so these tests depend on no real domain model.</summary>
public class PagedRow
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
}
