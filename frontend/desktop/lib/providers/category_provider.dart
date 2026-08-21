import 'dart:async';
import 'dart:typed_data';

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

  /// Fired after any successful category mutation (insert/update/delete/icon change) — every
  /// screen/widget that independently fetches its own copy of the category list
  /// (CategoryMultiSelectFilter, ProductUpsertDialog's category dropdown) subscribes to this in
  /// initState() and refetches, so a category created/edited/deleted on CategoriesScreen shows up
  /// there without the user having to reopen the app. CategoriesScreen itself doesn't need to
  /// listen — it already calls _loadData() directly after its own mutations. A broadcast
  /// `StreamController` (not a full state-management package, per this app's own convention — see
  /// .claude/rules/21-frontend-desktop.md) is the minimal shape for a fire-and-forget
  /// "something changed, refetch" signal.
  static final categoryRefreshBus = StreamController<void>.broadcast();

  static void _notifyChanged() {
    if (!categoryRefreshBus.isClosed) categoryRefreshBus.add(null);
  }

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

    _notifyChanged();
    return CategoryResponse.fromJson(response.data as Map<String, dynamic>);
  }

  /// PUT /api/categories/:id — plain JSON, metadata only. Never touches the icon.
  Future<CategoryResponse> updateCategory(int id, CategoryUpdateRequest request) async {
    final response = await apiClient.put('categories/$id', data: request.toJson());

    if (!_isSuccess(response.statusCode)) _handleError(response);

    _notifyChanged();
    return CategoryResponse.fromJson(response.data as Map<String, dynamic>);
  }

  /// DELETE /api/categories/:id — overridden only to fire [categoryRefreshBus]; behavior is
  /// otherwise identical to the inherited BaseProvider.delete.
  @override
  Future<void> delete(int id, {dynamic data}) async {
    await super.delete(id, data: data);
    _notifyChanged();
  }

  /// POST /api/categories/:id/icon (multipart) — first-time icon upload. Fails
  /// with a conflict if the category already has one; use [replaceIcon] then.
  /// [iconBytes] is always PNG — it's already been through ImageCropDialog's forced square crop
  /// (which preserves the source format, and the file picker only allows .png) by the time it
  /// reaches here.
  Future<CategoryResponse> createIcon(int id, Uint8List iconBytes) async {
    final formData = FormData.fromMap({
      'Icon': MultipartFile.fromBytes(iconBytes, filename: 'icon.png', contentType: DioMediaType('image', 'png')),
    });

    final response = await apiClient.post('categories/$id/icon', data: formData);

    if (!_isSuccess(response.statusCode)) _handleError(response);

    _notifyChanged();
    return CategoryResponse.fromJson(response.data as Map<String, dynamic>);
  }

  /// PUT /api/categories/:id/icon (multipart) — replaces an existing icon in place.
  Future<CategoryResponse> replaceIcon(int id, Uint8List iconBytes) async {
    final formData = FormData.fromMap({
      'Icon': MultipartFile.fromBytes(iconBytes, filename: 'icon.png', contentType: DioMediaType('image', 'png')),
    });

    final response = await apiClient.put('categories/$id/icon', data: formData);

    if (!_isSuccess(response.statusCode)) _handleError(response);

    _notifyChanged();
    return CategoryResponse.fromJson(response.data as Map<String, dynamic>);
  }
}
