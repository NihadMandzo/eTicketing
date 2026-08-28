import 'package:dio/dio.dart';

import '../core/api_client.dart';
import '../models/api_error.dart';
import '../models/city.dart';
import '../models/responses/product_response.dart';
import '../models/responses/recommendation_response.dart';
import 'api_exception.dart';

/// Recommendation surfaces — mirrors `RecommendationService` in `frontend/web`. Same
/// `_isSuccess`/`_handleError` shape as [CatalogService], which is the convention every service in
/// this folder follows.
class RecommendationService {
  bool _isSuccess(int? code) => code != null && code >= 200 && code < 300;

  Never _handleError(Response response) {
    ApiError apiError;
    try {
      final body = response.data;
      apiError = body is Map<String, dynamic> ? ApiError.fromJson(body) : ApiError(message: body?.toString());
    } catch (_) {
      apiError = ApiError(message: response.data?.toString());
    }
    throw ApiException(statusCode: response.statusCode ?? 0, apiError: apiError);
  }

  /// GET /api/recommendations/me — requires a session. Never comes back empty for lack of
  /// history: the backend falls through to popularity and says so in `source`, which is what the
  /// row title is derived from.
  Future<RecommendationResponse> getForMe({int take = 8}) async {
    final response = await apiClient.get('Recommendations/me', queryParameters: {'take': take});
    if (!_isSuccess(response.statusCode)) _handleError(response);
    return RecommendationResponse.fromJson(response.data as Map<String, dynamic>);
  }

  /// GET /api/recommendations/similar/{productId} — public, needs no user history.
  Future<List<ProductResponse>> getSimilar(String productId, {int take = 6}) async {
    final response = await apiClient.get(
      'Recommendations/similar/$productId',
      queryParameters: {'take': take},
    );
    if (!_isSuccess(response.statusCode)) _handleError(response);
    return (response.data as List<dynamic>)
        .map((item) => ProductResponse.fromJson(item as Map<String, dynamic>))
        .toList();
  }

  /// GET /api/recommendations/popular — public.
  Future<List<ProductResponse>> getPopular({City? city, int take = 8}) async {
    final response = await apiClient.get('Recommendations/popular', queryParameters: {
      'take': take,
      if (city != null) 'city': cityToJson(city),
    });
    if (!_isSuccess(response.statusCode)) _handleError(response);
    return (response.data as List<dynamic>)
        .map((item) => ProductResponse.fromJson(item as Map<String, dynamic>))
        .toList();
  }

  /// POST /api/recommendations/views — fire-and-forget. Swallows every failure on purpose:
  /// recording a view is a background nicety, and nothing about someone opening a product should
  /// break because the recommender is unreachable.
  Future<void> trackView(String productId) async {
    try {
      await apiClient.post('Recommendations/views', data: {'productId': productId});
    } catch (_) {
      // Intentionally ignored — see doc comment.
    }
  }
}
