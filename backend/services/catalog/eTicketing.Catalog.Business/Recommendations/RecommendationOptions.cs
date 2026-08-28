namespace eTicketing.Catalog.Business.Recommendations;

/// <summary>Bound from the "Recommendations" section of appsettings.json.</summary>
public sealed class RecommendationOptions
{
    /// <summary>Hour of day (0–23, server local time) the nightly retrain runs. Matrix
    /// factorization cannot learn incrementally — there is no "apply this one purchase" call, the
    /// whole model is rebuilt from all interactions at once — so "keeps learning" is implemented as
    /// a scheduled full retrain plus the manual POST /recommendations/retrain trigger.</summary>
    public int TrainAtHour { get; set; } = 3;

    /// <summary>Below this many interactions a user gets the content-based path instead of the
    /// model. Matrix factorization has nothing useful to say about someone who has touched one or
    /// two products — it would rank them off noise, which looks worse than an honest
    /// "same category as the thing you looked at".</summary>
    public int MinInteractionsForPersonalized { get; set; } = 3;

    /// <summary>Hard cap on the published products ranked per request. Scoring happens in memory
    /// (model predictions aren't expressible in SQL), so this is what bounds that work.</summary>
    public int MaxCandidates { get; set; } = 500;

    /// <summary>Private blob container the serialized model lives in. Private, unlike
    /// "product-images"/"category-icons": it is derived from users' purchase behavior.</summary>
    public string ModelContainer { get; set; } = "ml-models";
}
