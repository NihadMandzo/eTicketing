import 'package:dio/dio.dart';

import '../core/api_client.dart';
import '../models/api_error.dart';
import '../models/requests/hold_sector_request.dart';
import '../models/responses/hold_sector_response.dart';
import '../models/responses/paged_result.dart';
import '../models/responses/sector_response.dart';
import 'api_exception.dart';

/// Sector browsing + the Redis atomic hold — mirrors `SectorService` in
/// `frontend/web`.
class SectorService {
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

  /// GET /api/sectors?productId= — Published only, public.
  Future<PagedResult<SectorResponse>> getSectors(String productId) async {
    final response = await apiClient.get('Sectors', queryParameters: {'productId': productId, 'pageSize': 100});
    if (!_isSuccess(response.statusCode)) _handleError(response);
    return PagedResult.fromJson(response.data as Map<String, dynamic>, SectorResponse.fromJson);
  }

  /// POST /api/sectors/{id}/hold — Redis atomic decrement/lock, TTL 5 min.
  /// Requires auth — callers should route to LoginScreen on a 401.
  Future<HoldSectorResponse> hold(String sectorId, HoldSectorRequest request) async {
    final response = await apiClient.post('Sectors/$sectorId/hold', data: request.toJson());
    if (!_isSuccess(response.statusCode)) _handleError(response);
    return HoldSectorResponse.fromJson(response.data as Map<String, dynamic>);
  }

  /// POST /api/sectors/holds/{holdId}/release — best-effort early release of a still-live hold
  /// (e.g. the buyer switched to a different spot/date, or a sibling hold in the same cart failed
  /// to purchase). Deliberately swallows every failure — callers only ever want to give capacity
  /// back on a best-effort basis, never to block on it or surface an error for a release the buyer
  /// didn't directly ask for.
  Future<void> release(String holdId) async {
    try {
      await apiClient.post('Sectors/holds/$holdId/release');
    } catch (_) {
      // Best-effort — an already-expired holdId, or a transient network error, is fine to ignore.
    }
  }
}
