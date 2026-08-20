import 'package:dio/dio.dart';

import '../core/api_client.dart';
import '../models/api_error.dart';
import '../models/responses/category_response.dart';
import '../models/responses/paged_result.dart';
import '../models/responses/product_response.dart';
import 'api_exception.dart';

/// Public catalog browsing — Categories/Products, no auth required (mirrors
/// `CatalogService` in `frontend/web`).
class CatalogService {
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

  Future<List<CategoryResponse>> getCategories() async {
    final response = await apiClient.get('Categories');
    if (!_isSuccess(response.statusCode)) _handleError(response);
    return (response.data as List).map((e) => CategoryResponse.fromJson(e as Map<String, dynamic>)).toList();
  }

  /// GET /api/products — Published only, public.
  Future<PagedResult<ProductResponse>> getProducts({int page = 0, int pageSize = 20, String? fts, int? categoryId}) async {
    final response = await apiClient.get('Products', queryParameters: {
      'page': page,
      'pageSize': pageSize,
      if (fts != null && fts.isNotEmpty) 'fts': fts,
      if (categoryId != null) 'categoryId': categoryId,
    });
    if (!_isSuccess(response.statusCode)) _handleError(response);
    return PagedResult.fromJson(response.data as Map<String, dynamic>, ProductResponse.fromJson);
  }

  /// GET /api/products/{id} — Published only, public (404 for Draft/unknown).
  Future<ProductResponse> getProductById(String id) async {
    final response = await apiClient.get('Products/$id');
    if (!_isSuccess(response.statusCode)) _handleError(response);
    return ProductResponse.fromJson(response.data as Map<String, dynamic>);
  }
}
