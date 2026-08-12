import '../core/api_client.dart';
import '../models/api_error.dart';
import 'api_exception.dart';

/// GET /api/events/organization-ids?categoryIds=1,2,3 — resolves the
/// organization ids that have events in the given categories (any status),
/// backing the organizations list's category multiselect filter. Not a
/// paged/flat resource, so it's hand-rolled like OrganizationUsersProvider
/// rather than extending BaseProvider.
class EventOrganizationIdsProvider {
  Future<List<String>> getOrganizationIds({required List<int> categoryIds}) async {
    final response = await apiClient.get(
      'events/organization-ids',
      queryParameters: {'categoryIds': categoryIds.join(',')},
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

    return (response.data as List).cast<String>();
  }
}
