namespace eTicketing.Ticketing.Business.Analytics;

/// <summary>
/// Which strategy actually produced a block of the AI Uvidi tab.
///
/// Every block carries its own copy rather than the response carrying one for all of them: on a
/// young organization the forecast can legitimately be <see cref="Heuristic"/> while segmentation
/// is <see cref="Insufficient"/> and nothing about that is an error. The clients title each block
/// from this, which is the same reason RecommendationSource travels on the wire — a fallback that
/// cannot be seen looks like a bug when a demo shows something unexpected.
///
/// Serialized as the integer ordinal, like every other enum crossing this boundary, and mirrored
/// by ordinal in the frontend enum tables. Append only.
/// </summary>
public enum AnalyticsSource
{
    /// <summary>A fitted ML.NET model produced this block.</summary>
    Model,

    /// <summary>Not enough history for the model, but enough to say something honest — a moving
    /// average, a robust z-score, fixed RFM tiers.</summary>
    Heuristic,

    /// <summary>Too little data to claim anything at all. The block is empty and the UI says so
    /// rather than drawing an empty chart.</summary>
    Insufficient
}
