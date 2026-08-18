import '../core/api_client.dart';
import '../models/api_error.dart';
import '../models/responses/paged_result.dart';
import '../models/responses/product_response.dart';
import '../models/search_objects/base_search_object.dart';
import 'api_exception.dart';

/// GET /api/products/all?OrganizationId=... — backs the organization detail
/// screen's Products tab (and its product-count stat card, via the response's
/// totalCount). Not a nested `/organizations/{id}/products` route — Catalog
/// (Products) and Identity (Organizations) are separate services/databases,
/// so no such nested route exists; this hits Catalog's flat `/products/all`
/// with an OrganizationId filter merged into the query, mirroring how
/// OrganizationUsersProvider hits Identity's nested route for admins/
/// superadmins.
class OrganizationProductsProvider {
  static const String _extension = 'products/all';

  Future<PagedResult<ProductResponse>> getAll({
    required String organizationId,
    BaseSearchObject? searchObject,
    required ProductResponse Function(Map<String, dynamic>) fromJson,
  }) async {
    final queryParams = <String, dynamic>{
      ...?searchObject?.toQueryString(),
      'OrganizationId': organizationId,
    };

    final response = await apiClient.get(_extension, queryParameters: queryParams);

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
