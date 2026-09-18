using eTicketing.Catalog.Data.Entities;
using Mapster;

namespace eTicketing.Catalog.Business.Recommendations.Mapping;

/// <summary>
/// The recommender's back-office shape. A pure 1:1 projection of the snapshot row — the trained
/// model's bytes live in the private <c>ml-models</c> blob container and are deliberately not on
/// the entity at all, so there is nothing here to exclude.
/// </summary>
public class RecommendationMappingConfig : IRegister
{
    public void Register(TypeAdapterConfig config)
    {
        config.NewConfig<RecommendationModelSnapshot, ModelSnapshotResponse>();
    }
}
