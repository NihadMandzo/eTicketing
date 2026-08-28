import '../api_enum.dart';
import 'product_response.dart';

/// Which strategy produced a list. Same ordinal/name convention as every other enum crossing this
/// boundary — the wire value is the integer ordinal, see [enumNameFromJson].
enum RecommendationSource { personalized, contentBased, popular }

/// Declaration order must match
/// `eTicketing.Catalog.Business.Recommendations.RecommendationSource` exactly.
const recommendationSourceNames = ['Personalized', 'ContentBased', 'Popular'];

RecommendationSource recommendationSourceFromJson(dynamic value) =>
    switch (enumNameFromJson(value, recommendationSourceNames, 'Popular')) {
      'Personalized' => RecommendationSource.personalized,
      'ContentBased' => RecommendationSource.contentBased,
      _ => RecommendationSource.popular,
    };

/// Row heading per strategy. Personalized and ContentBased deliberately share one: the distinction
/// is real to us but meaningless to a shopper, who only needs to know the row is about them.
String recommendationTitle(RecommendationSource source) => switch (source) {
      RecommendationSource.personalized => 'Preporučeno za vas',
      RecommendationSource.contentBased => 'Preporučeno za vas',
      RecommendationSource.popular => 'Popularno u vašem gradu',
    };

class RecommendationResponse {
  final List<ProductResponse> items;
  final RecommendationSource source;

  const RecommendationResponse({required this.items, required this.source});

  factory RecommendationResponse.fromJson(Map<String, dynamic> json) => RecommendationResponse(
        items: (json['items'] as List<dynamic>? ?? [])
            .map((item) => ProductResponse.fromJson(item as Map<String, dynamic>))
            .toList(),
        source: recommendationSourceFromJson(json['source']),
      );
}
