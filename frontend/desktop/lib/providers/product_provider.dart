import 'package:dio/dio.dart';

import '../core/api_client.dart';
import '../models/api_error.dart';
import '../models/requests/product_upsert_request.dart';
import '../models/responses/paged_result.dart';
import '../models/responses/product_preview_response.dart';
import '../models/responses/product_response.dart';
import '../models/search_objects/product_search_object.dart';
import 'api_exception.dart';
import 'base_provider.dart';

class ProductProvider extends BaseProvider<ProductResponse, String> {
  ProductProvider() : super('products');

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

  Future<ProductResponse> createProduct(ProductUpsertRequest request) =>
      insert(request.toJson(), fromJson: ProductResponse.fromJson);

  Future<ProductResponse> updateProduct(String id, ProductUpsertRequest request) =>
      update(id, request.toJson(), fromJson: ProductResponse.fromJson);

  /// GET /api/products/mine — organizer's own organization's products, all statuses. Not covered
  /// by the inherited getAll() (that hits the plain "products" — Published-only — route).
  Future<PagedResult<ProductResponse>> getMine({ProductSearchObject? searchObject}) async {
    final response =
        await apiClient.get('products/mine', queryParameters: searchObject?.toQueryString());

    if (!_isSuccess(response.statusCode)) _handleError(response);

    return PagedResult.fromJson(response.data as Map<String, dynamic>, ProductResponse.fromJson);
  }

  /// POST /api/products/preview — stateless, no DB write.
  Future<ProductPreviewResponse> preview(ProductUpsertRequest request) async {
    final response = await apiClient.post('products/preview', data: request.toJson());

    if (!_isSuccess(response.statusCode)) _handleError(response);

    return ProductPreviewResponse.fromJson(response.data as Map<String, dynamic>);
  }

  /// POST /api/products/{id}/publish — Draft → Published.
  Future<ProductResponse> publish(String id) async {
    final response = await apiClient.post('products/$id/publish');

    if (!_isSuccess(response.statusCode)) _handleError(response);

    return ProductResponse.fromJson(response.data as Map<String, dynamic>);
  }
}
