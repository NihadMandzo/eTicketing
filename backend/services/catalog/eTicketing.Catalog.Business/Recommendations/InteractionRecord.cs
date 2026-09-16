namespace eTicketing.Catalog.Business.Recommendations;

/// <summary>One row as ML.NET sees it. Guids travel as strings because MapValueToKey builds its
/// dictionary over a text column.</summary>
public sealed class InteractionRecord
{
    public string UserId { get; set; } = string.Empty;
    public string ProductId { get; set; } = string.Empty;
    public float Label { get; set; }
}
