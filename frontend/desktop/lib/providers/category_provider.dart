import 'dart:convert';
import 'dart:io';

import 'package:http/http.dart' as http;

import '../models/api_error.dart';
import '../models/requests/category_insert_request.dart';
import '../models/requests/category_update_request.dart';
import '../models/responses/category_response.dart';
import 'api_exception.dart';
import 'authorization.dart';
import 'base_provider.dart';

class CategoryProvider extends BaseProvider<CategoryResponse> {
  CategoryProvider() : super('Categories');

  Map<String, String> _authHeaders() {
    if (Authorization.token != null && Authorization.token!.isNotEmpty) {
      return {'Authorization': 'Bearer ${Authorization.token}'};
    }
    return {};
  }

  bool _isSuccess(int code) => code >= 200 && code < 300;

  Never _handleError(http.Response response) {
    ApiError apiError;
    try {
      final body = jsonDecode(response.body) as Map<String, dynamic>;
      apiError = ApiError.fromJson(body);
    } catch (_) {
      apiError = ApiError(errorCode: response.body);
    }
    throw ApiException(statusCode: response.statusCode, apiError: apiError);
  }

  /// POST /api/Categories  (multipart – icon is required)
  Future<CategoryResponse> insertCategory(
    CategoryInsertRequest request, {
    required File iconFile,
  }) async {
    final uri = Uri.parse('${BaseProvider.baseUrl}Categories');
    final multipart = http.MultipartRequest('POST', uri)
      ..headers.addAll(_authHeaders())
      ..fields.addAll(request.toFields())
      ..files.add(await http.MultipartFile.fromPath('Icon', iconFile.path));

    final streamed = await multipart.send().timeout(const Duration(seconds: 15));
    final response = await http.Response.fromStream(streamed);

    if (!_isSuccess(response.statusCode)) _handleError(response);

    return CategoryResponse.fromJson(
      jsonDecode(response.body) as Map<String, dynamic>,
    );
  }

  /// PUT /api/Categories/:id  (multipart – icon optional)
  Future<CategoryResponse> updateCategory(
    int id,
    CategoryUpdateRequest request, {
    File? iconFile,
  }) async {
    final uri = Uri.parse('${BaseProvider.baseUrl}Categories/$id');
    final multipart = http.MultipartRequest('PUT', uri)
      ..headers.addAll(_authHeaders())
      ..fields.addAll(request.toFields());

    if (iconFile != null) {
      multipart.files.add(
        await http.MultipartFile.fromPath('Icon', iconFile.path),
      );
    }

    final streamed = await multipart.send().timeout(const Duration(seconds: 15));
    final response = await http.Response.fromStream(streamed);

    if (!_isSuccess(response.statusCode)) _handleError(response);

    return CategoryResponse.fromJson(
      jsonDecode(response.body) as Map<String, dynamic>,
    );
  }
}
