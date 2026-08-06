import '../core/api_client.dart';
import '../models/api_error.dart';
import '../models/responses/admin_user_response.dart';
import '../models/responses/paged_result.dart';
import '../models/search_objects/base_search_object.dart';
import 'api_exception.dart';
import 'base_provider.dart';

/// Provider for GET/POST/DELETE /api/admins (SuperAdmin users list).
class AdminProvider extends BaseProvider<AdminUserResponse, String> {
  AdminProvider() : super('admins');
}

/// Provider for the nested GET /api/organizations/{organizationId}/users
/// endpoint (org users list) — not a flat resource, so it can't extend
/// [BaseProvider] the same way [AdminProvider] does.
class OrganizationUsersProvider {
  static const String _extension = 'organizations';

  Future<PagedResult<AdminUserResponse>> getAll({
    required String organizationId,
    BaseSearchObject? searchObject,
    required AdminUserResponse Function(Map<String, dynamic>) fromJson,
  }) async {
    final queryParams = searchObject?.toQueryString() ?? {};

    final response = await apiClient.get(
      '$_extension/$organizationId/users',
      queryParameters: queryParams.isNotEmpty ? queryParams : null,
    );

    if (response.statusCode == null || response.statusCode! < 200 || response.statusCode! >= 300) {
      ApiError apiError;
      try {
        final body = response.data;
        apiError = body is Map<String, dynamic> ? ApiError.fromJson(body) : ApiError(message: body?.toString());
      } catch (_) {
        apiError = ApiError(message: response.data?.toString());
      }
      throw ApiException(statusCode: response.statusCode ?? 0, apiError: apiError);
    }

    return PagedResult.fromJson(response.data as Map<String, dynamic>, fromJson);
  }
}
