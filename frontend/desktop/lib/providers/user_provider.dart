import '../core/api_client.dart';
import '../models/api_error.dart';
import '../models/responses/admin_user_response.dart';
import '../models/responses/paged_result.dart';
import '../models/search_objects/base_search_object.dart';
import 'api_exception.dart';
import 'base_provider.dart';

/// Provider for GET/PUT/DELETE /api/admins (SuperAdmin-facing "platform Users"
/// list — despite the "admins" name, this now spans every non-buyer role:
/// SuperAdmin, Admin, OrganizationSuperAdmin, OrganizationAdmin. See
/// AdminService.GetAsync on the backend for the same "Admin"-named-but-broader
/// note).
class AdminProvider extends BaseProvider<AdminUserResponse, String> {
  AdminProvider() : super('admins');
}

/// Provider for the nested /api/organizations/{organizationId}/users
/// endpoints (org users list/add/edit/remove) — not a flat resource, so it
/// can't extend [BaseProvider] the same way [AdminProvider] does.
class OrganizationUsersProvider {
  static const String _extension = 'organizations';

  Never _handleError(dynamic response) {
    ApiError apiError;
    try {
      final body = response.data;
      apiError = body is Map<String, dynamic> ? ApiError.fromJson(body) : ApiError(message: body?.toString());
    } catch (_) {
      apiError = ApiError(message: response.data?.toString());
    }
    throw ApiException(statusCode: response.statusCode ?? 0, apiError: apiError);
  }

  bool _isSuccess(int? statusCode) => statusCode != null && statusCode >= 200 && statusCode < 300;

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

    if (!_isSuccess(response.statusCode)) _handleError(response);

    return PagedResult.fromJson(response.data as Map<String, dynamic>, fromJson);
  }

  Future<AdminUserResponse> insert(
    String organizationId,
    dynamic request, {
    required AdminUserResponse Function(Map<String, dynamic>) fromJson,
  }) async {
    final response = await apiClient.post('$_extension/$organizationId/users', data: request);

    if (!_isSuccess(response.statusCode)) _handleError(response);

    return fromJson(response.data as Map<String, dynamic>);
  }

  Future<AdminUserResponse> update(
    String organizationId,
    String userId,
    dynamic request, {
    required AdminUserResponse Function(Map<String, dynamic>) fromJson,
  }) async {
    final response = await apiClient.put('$_extension/$organizationId/users/$userId', data: request);

    if (!_isSuccess(response.statusCode)) _handleError(response);

    return fromJson(response.data as Map<String, dynamic>);
  }

  Future<void> delete(String organizationId, String userId) async {
    final response = await apiClient.delete('$_extension/$organizationId/users/$userId');

    if (!_isSuccess(response.statusCode)) _handleError(response);
  }
}
