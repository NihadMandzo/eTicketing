import 'dart:async';

import 'package:dio/dio.dart';

import '../core/api_client.dart';
import '../core/api_config.dart';
import '../models/api_error.dart';
import '../models/responses/paged_result.dart';
import '../models/search_objects/base_search_object.dart';
import 'api_exception.dart';

/// Generic CRUD provider for a resource whose id is of type [TId] — `String`
/// (a GUID) for Identity-owned resources (users, organizations), `int` for
/// resources that are still SQL-identity-keyed (e.g. categories).
///
/// Auth is handled transparently: the shared [apiClient] attaches the
/// httpOnly session cookie to every request via its cookie jar, so no
/// provider ever needs to read or set a token itself.
class BaseProvider<T, TId> {
  static String get baseUrl => ApiConfig.baseUrl;

  final String _extension;

  BaseProvider(this._extension);

  Never _handleError(Response response) {
    ApiError apiError;
    try {
      final body = response.data;
      apiError = body is Map<String, dynamic>
          ? ApiError.fromJson(body)
          : ApiError(message: body?.toString());
    } catch (_) {
      apiError = ApiError(message: response.data?.toString());
    }
    throw ApiException(statusCode: response.statusCode ?? 0, apiError: apiError);
  }

  bool _isSuccess(int? statusCode) => statusCode != null && statusCode >= 200 && statusCode < 300;

  Future<Response> _send(Future<Response> Function() request) async {
    try {
      return await request();
    } on DioException catch (e) {
      if (e.type == DioExceptionType.connectionTimeout ||
          e.type == DioExceptionType.receiveTimeout ||
          e.type == DioExceptionType.sendTimeout) {
        throw ApiException(
          statusCode: 408,
          apiError: ApiError(message: 'Zahtjev je istekao (timeout).'),
        );
      }
      rethrow;
    }
  }

  Future<PagedResult<T>> getAll({
    BaseSearchObject? searchObject,
    required T Function(Map<String, dynamic>) fromJson,
  }) async {
    final queryParams = searchObject?.toQueryString() ?? {};

    final response = await _send(() => apiClient.get(
          _extension,
          queryParameters: queryParams.isNotEmpty ? queryParams : null,
        ));

    if (!_isSuccess(response.statusCode)) _handleError(response);

    return PagedResult.fromJson(response.data as Map<String, dynamic>, fromJson);
  }

  Future<T> getById(
    TId id, {
    required T Function(Map<String, dynamic>) fromJson,
  }) async {
    final response = await _send(() => apiClient.get('$_extension/$id'));

    if (!_isSuccess(response.statusCode)) _handleError(response);

    return fromJson(response.data as Map<String, dynamic>);
  }

  Future<T> insert(
    dynamic request, {
    required T Function(Map<String, dynamic>) fromJson,
  }) async {
    final response = await _send(() => apiClient.post(_extension, data: request));

    if (!_isSuccess(response.statusCode)) _handleError(response);

    return fromJson(response.data as Map<String, dynamic>);
  }

  Future<T> update(
    TId id,
    dynamic request, {
    required T Function(Map<String, dynamic>) fromJson,
  }) async {
    final response = await _send(() => apiClient.put('$_extension/$id', data: request));

    if (!_isSuccess(response.statusCode)) _handleError(response);

    return fromJson(response.data as Map<String, dynamic>);
  }

  Future<void> delete(TId id) async {
    final response = await _send(() => apiClient.delete('$_extension/$id'));

    if (!_isSuccess(response.statusCode)) _handleError(response);
  }
}
