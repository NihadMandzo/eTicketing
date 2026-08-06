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

  /// POST /api/categories (multipart – icon is required)
  Future<CategoryResponse> insertCategory(
    CategoryInsertRequest request, {
    required File iconFile,
  }) async {
    final formData = FormData.fromMap({
      ...request.toFields(),
      'Icon': await MultipartFile.fromFile(iconFile.path),
    });

    final response = await apiClient.post('categories', data: formData);

    if (!_isSuccess(response.statusCode)) _handleError(response);

    return CategoryResponse.fromJson(response.data as Map<String, dynamic>);
  }

  /// PUT /api/categories/:id (multipart – icon optional)
  Future<CategoryResponse> updateCategory(
    int id,
    CategoryUpdateRequest request, {
    File? iconFile,
  }) async {
    final formData = FormData.fromMap({
      ...request.toFields(),
      if (iconFile != null) 'Icon': await MultipartFile.fromFile(iconFile.path),
    });

    final response = await apiClient.put('categories/$id', data: formData);

    if (!_isSuccess(response.statusCode)) _handleError(response);

    return CategoryResponse.fromJson(response.data as Map<String, dynamic>);
  }
}
