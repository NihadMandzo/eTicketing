import 'package:dio/dio.dart';

import '../core/api_client.dart';
import '../models/api_error.dart';
import '../models/responses/paged_result.dart';
import '../models/responses/subscription_response.dart';
import 'api_exception.dart';

/// The buyer's own recurring reservations (monthly parking). Mirrors `frontend/web`'s
/// SubscriptionService.
class SubscriptionService {
  bool _isSuccess(int? code) => code != null && code >= 200 && code < 300;

  Never _handleError(Response response) {
    ApiError apiError;
    try {
      final body = response.data;
      apiError = body is Map<String, dynamic>
          ? ApiError.fromJson(body)
          : ApiError(message: body?.toString());
    } catch (_) {
      apiError = ApiError(message: response.data?.toString());
    }
    throw ApiException(statusCode: response.statusCode ?? 0, apiError: apiError);
  }

  /// GET /api/subscriptions/mine
  Future<PagedResult<SubscriptionResponse>> getMine({int page = 0, int pageSize = 20}) async {
    final response = await apiClient.get(
      'Subscriptions/mine',
      queryParameters: {'page': page, 'pageSize': pageSize},
    );
    if (!_isSuccess(response.statusCode)) _handleError(response);
    return PagedResult.fromJson(response.data as Map<String, dynamic>, SubscriptionResponse.fromJson);
  }

  /// POST /api/subscriptions/{id}/cancel — schedules cancellation at the end of the paid-for
  /// period. The subscription stays active until the provider confirms it has actually ended, so
  /// the buyer keeps the month they already paid for.
  Future<void> cancel(String id) async {
    final response = await apiClient.post('Subscriptions/$id/cancel');
    if (!_isSuccess(response.statusCode)) _handleError(response);
  }
}
