namespace eTicketing.Ticketing.Business.Sectors;

/// <summary>Same shape for create and update.</summary>
public record UpsertSectorRequest
{
    public Guid ProductId { get; init; }
    public string Name { get; init; } = string.Empty;
    public int Capacity { get; init; }
    public decimal Price { get; init; }
    public int? PeriodYear { get; init; }
    public int? PeriodMonth { get; init; }
}
