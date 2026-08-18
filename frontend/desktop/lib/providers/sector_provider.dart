import 'package:dio/dio.dart';

import '../core/api_client.dart';
import '../models/api_error.dart';
import '../models/requests/sector_upsert_request.dart';
import '../models/responses/paged_result.dart';
import '../models/responses/sector_preview_response.dart';
import '../models/responses/sector_response.dart';
import '../models/search_objects/sector_search_object.dart';
import 'api_exception.dart';
import 'base_provider.dart';

class SectorProvider extends BaseProvider<SectorResponse, String> {
  SectorProvider() : super('sectors');

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

  bool _isSuccess(int? code) => code != null && code >= 200 && code < 300;

  Future<SectorResponse> createSector(SectorUpsertRequest request) =>
      insert(request.toJson(), fromJson: SectorResponse.fromJson);

  Future<SectorResponse> updateSector(String id, SectorUpsertRequest request) =>
      update(id, request.toJson(), fromJson: SectorResponse.fromJson);

  /// GET /api/sectors/mine — organizer's own organization's sectors, any status.
  Future<PagedResult<SectorResponse>> getMine({SectorSearchObject? searchObject}) async {
    final response =
        await apiClient.get('sectors/mine', queryParameters: searchObject?.toQueryString());

    if (!_isSuccess(response.statusCode)) _handleError(response);

    return PagedResult.fromJson(response.data as Map<String, dynamic>, SectorResponse.fromJson);
  }

  /// POST /api/sectors/preview — stateless, no DB write.
  Future<SectorPreviewResponse> preview(SectorUpsertRequest request) async {
    final response = await apiClient.post('sectors/preview', data: request.toJson());

    if (!_isSuccess(response.statusCode)) _handleError(response);

    return SectorPreviewResponse.fromJson(response.data as Map<String, dynamic>);
  }

  /// POST /api/sectors/{id}/publish — Draft → Published.
  Future<SectorResponse> publish(String id) async {
    final response = await apiClient.post('sectors/$id/publish');

    if (!_isSuccess(response.statusCode)) _handleError(response);

    return SectorResponse.fromJson(response.data as Map<String, dynamic>);
  }
}
