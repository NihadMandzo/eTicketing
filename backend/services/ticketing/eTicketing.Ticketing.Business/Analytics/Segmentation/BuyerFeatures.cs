namespace eTicketing.Ticketing.Business.Analytics.Segmentation;

/// <summary>One buyer as ML.NET sees them: five numeric features and nothing identifying. The
/// UserId stays outside the model — it is a key, not a signal, and feeding it in would let the
/// clusterer split on an arbitrary Guid ordering.</summary>
internal sealed class BuyerFeatures
{
    public float RecencyDays { get; set; }
    public float Orders { get; set; }
    public float Spend { get; set; }
    public float AverageTicketPrice { get; set; }
    public float TicketsPerOrder { get; set; }
}
