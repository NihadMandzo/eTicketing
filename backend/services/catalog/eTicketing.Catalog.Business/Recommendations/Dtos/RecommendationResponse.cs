using eTicketing.Catalog.Business.Products;
using eTicketing.Contracts.Persistence;

namespace eTicketing.Catalog.Business.Recommendations;

public record RecommendationResponse(IReadOnlyList<ProductResponse> Items, RecommendationSource Source);
