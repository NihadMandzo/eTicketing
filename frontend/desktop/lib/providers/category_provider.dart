import 'dart:io';

import 'package:dio/dio.dart';

import '../core/api_client.dart';
import '../models/api_error.dart';
import '../models/requests/category_insert_request.dart';
import '../models/requests/category_update_request.dart';
import '../models/responses/category_response.dart';
import 'api_exception.dart';
import 'base_provider.dart';

class CategoryProvider extends BaseProvider<CategoryResponse, int> {
  CategoryProvider() : super('categories');

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

  /// POST /api/categories — plain JSON, metadata only. The icon (if any) is
  /// uploaded separately afterwards via [createIcon].
  Future<CategoryResponse> insertCategory(CategoryInsertRequest request) async {
    final response = await apiClient.post('categories', data: request.toJson());

    if (!_isSuccess(response.statusCode)) _handleError(response);

    return CategoryResponse.fromJson(response.data as Map<String, dynamic>);
  }

  /// PUT /api/categories/:id — plain JSON, metadata only. Never touches the icon.
  Future<CategoryResponse> updateCategory(int id, CategoryUpdateRequest request) async {
    final response = await apiClient.put('categories/$id', data: request.toJson());

    if (!_isSuccess(response.statusCode)) _handleError(response);

    return CategoryResponse.fromJson(response.data as Map<String, dynamic>);
  }

  /// POST /api/categories/:id/icon (multipart) — first-time icon upload. Fails
  /// with a conflict if the category already has one; use [replaceIcon] then.
  Future<CategoryResponse> createIcon(int id, File iconFile) async {
    final formData = FormData.fromMap({
      'Icon': await MultipartFile.fromFile(iconFile.path, contentType: DioMediaType('image', 'png')),
    });

    final response = await apiClient.post('categories/$id/icon', data: formData);

    if (!_isSuccess(response.statusCode)) _handleError(response);

    return CategoryResponse.fromJson(response.data as Map<String, dynamic>);
  }

  /// PUT /api/categories/:id/icon (multipart) — replaces an existing icon in place.
  Future<CategoryResponse> replaceIcon(int id, File iconFile) async {
    final formData = FormData.fromMap({
      'Icon': await MultipartFile.fromFile(iconFile.path, contentType: DioMediaType('image', 'png')),
    });

    final response = await apiClient.put('categories/$id/icon', data: formData);

    if (!_isSuccess(response.statusCode)) _handleError(response);

    return CategoryResponse.fromJson(response.data as Map<String, dynamic>);
  }
}
