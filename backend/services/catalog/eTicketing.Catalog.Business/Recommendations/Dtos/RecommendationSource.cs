namespace eTicketing.Catalog.Business.Recommendations;

/// <summary>Which of the three ranking strategies actually produced a list. Persisted nowhere and
/// travelling on the wire deliberately: the frontends title the row from it ("Preporučeno za vas"
/// vs "Popularno"), and it makes the fallback chain observable instead of guesswork
/// when a demo shows something unexpected.
///
/// Serialized as the integer ordinal, like every other enum crossing this boundary — mirrored by
/// ordinal in the frontend enum tables. Append only.</summary>
public enum RecommendationSource
{
    /// <summary>Ranked by the trained matrix-factorization model.</summary>
    Personalized,

    /// <summary>The user has some history but not enough for the model (or the model has never
    /// seen them) — ranked by similarity to what they already touched.</summary>
    ContentBased,

    /// <summary>No usable history at all — ranked by how many people bought each product.</summary>
    Popular
}
