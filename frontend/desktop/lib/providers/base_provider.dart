import 'dart:convert';

import 'package:http/http.dart' as http;

import '../models/api_error.dart';
import '../models/responses/paged_result.dart';
import '../models/search_objects/base_search_object.dart';
import 'api_exception.dart';
import 'authorization.dart';

class BaseProvider<T> {
  static const String baseUrl = String.fromEnvironment('API_BASE_URL', defaultValue: 'http://localhost:5189/api/');

  final String _extension;

  BaseProvider(this._extension);

  Map<String, String> _getHeaders() {
    final headers = {
      'Content-Type': 'application/json',
    };

    if (Authorization.token != null && Authorization.token!.isNotEmpty) {
      headers['Authorization'] = 'Bearer ${Authorization.token}';
    }

    return headers;
  }

  // Parses a non-2xx response into a typed ApiException.
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

  bool _isSuccess(int statusCode) =>
      statusCode >= 200 && statusCode < 300;

  Future<PagedResult<T>> getAll({
    BaseSearchObject? searchObject,
    required T Function(Map<String, dynamic>) fromJson,
  }) async {
    final queryParams = searchObject?.toQueryString() ?? {};

    final uri = Uri.parse('$baseUrl$_extension')
        .replace(queryParameters: queryParams.isNotEmpty ? queryParams : null);

    final response = await http.get(uri, headers: _getHeaders()).timeout(const Duration(seconds: 10));

    if (!_isSuccess(response.statusCode)) _handleError(response);

    try {
      final data = jsonDecode(response.body) as Map<String, dynamic>;
      return PagedResult.fromJson(data, fromJson);
    } catch (e) {
      throw FormatException('Failed to parse response JSON: $e');
    }
  }

  Future<T> getById(
    int id, {
    required T Function(Map<String, dynamic>) fromJson,
  }) async {
    final uri = Uri.parse('$baseUrl$_extension/$id');

    final response = await http.get(uri, headers: _getHeaders()).timeout(const Duration(seconds: 10));

    if (!_isSuccess(response.statusCode)) _handleError(response);

    try {
      final data = jsonDecode(response.body) as Map<String, dynamic>;
      return fromJson(data);
    } catch (e) {
      throw FormatException('Failed to parse response JSON: $e');
    }
  }

  Future<T> insert(
    dynamic request, {
    required T Function(Map<String, dynamic>) fromJson,
  }) async {
    final uri = Uri.parse('$baseUrl$_extension');

    final response = await http.post(
      uri,
      headers: _getHeaders(),
      body: jsonEncode(request),
    ).timeout(const Duration(seconds: 10));

    if (!_isSuccess(response.statusCode)) _handleError(response);

    try {
      final data = jsonDecode(response.body) as Map<String, dynamic>;
      return fromJson(data);
    } catch (e) {
      throw FormatException('Failed to parse response JSON: $e');
    }
  }

  Future<T> update(
    int id,
    dynamic request, {
    required T Function(Map<String, dynamic>) fromJson,
  }) async {
    final uri = Uri.parse('$baseUrl$_extension/$id');

    final response = await http.put(
      uri,
      headers: _getHeaders(),
      body: jsonEncode(request),
    ).timeout(const Duration(seconds: 10));

    if (!_isSuccess(response.statusCode)) _handleError(response);

    try {
      final data = jsonDecode(response.body) as Map<String, dynamic>;
      return fromJson(data);
    } catch (e) {
      throw FormatException('Failed to parse response JSON: $e');
    }
  }

  Future<void> delete(int id) async {
    final uri = Uri.parse('$baseUrl$_extension/$id');

    final response = await http.delete(uri, headers: _getHeaders()).timeout(const Duration(seconds: 10));

    if (!_isSuccess(response.statusCode)) _handleError(response);
  }
}
