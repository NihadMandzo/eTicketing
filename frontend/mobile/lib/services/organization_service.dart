import 'package:dio/dio.dart';

import '../core/api_client.dart';
import '../models/api_error.dart';
import '../models/responses/organization_response.dart';
import 'api_exception.dart';

/// GET /api/organizations/{id} is public — used only to show the
/// organizer's name on event/ticket detail screens.
class OrganizationService {
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

  Future<OrganizationResponse> getById(String id) async {
    final response = await apiClient.get('Organizations/$id');
    if (!_isSuccess(response.statusCode)) _handleError(response);
    return OrganizationResponse.fromJson(response.data as Map<String, dynamic>);
  }
}
