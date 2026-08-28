import 'package:dio/dio.dart';

import '../core/api_client.dart';
import '../models/api_error.dart';
import '../models/responses/model_status_response.dart';
import 'api_exception.dart';
import 'base_provider.dart';

/// Recommendation-model administration. Extends [BaseProvider] for its `send` wrapper (timeouts,
/// error normalization) rather than for its CRUD methods — the model is not a collection you
/// create and delete rows in, so `getAll`/`insert`/`update`/`delete` are simply unused here, same
/// as in [TicketPrintProvider].
class RecommendationProvider extends BaseProvider<ModelStatusResponse, String> {
  RecommendationProvider() : super('recommendations');

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

  /// GET /api/recommendations/status — PlatformStaff only.
  Future<ModelStatusResponse> getStatus() async {
    final response = await send(() => apiClient.get('recommendations/status'));

    if (!_isSuccess(response.statusCode)) _handleError(response);

    return ModelStatusResponse.fromJson(response.data as Map<String, dynamic>);
  }

  /// POST /api/recommendations/retrain — trains immediately instead of waiting for the nightly
  /// run, and returns the refreshed status. Returns 409 (`recommendation.no_training_data`) when
  /// nothing has been recorded yet; the screen surfaces that message as-is.
  Future<ModelStatusResponse> retrain() async {
    final response = await send(() => apiClient.post('recommendations/retrain'));

    if (!_isSuccess(response.statusCode)) _handleError(response);

    return ModelStatusResponse.fromJson(response.data as Map<String, dynamic>);
  }
}
