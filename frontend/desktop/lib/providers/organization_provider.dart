import 'dart:io';

import 'package:dio/dio.dart';

import '../core/api_client.dart';
import '../models/api_error.dart';
import '../models/requests/organization_insert_request.dart';
import '../models/requests/organization_update_request.dart';
import '../models/responses/organization_response.dart';
import 'api_exception.dart';
import 'base_provider.dart';

class OrganizationProvider extends BaseProvider<OrganizationResponse, String> {
  OrganizationProvider() : super('organizations');

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

  /// POST /api/organizations (multipart – logo is optional)
  Future<OrganizationResponse> insertOrganization(
    OrganizationInsertRequest request, {
    File? logoFile,
  }) async {
    final formData = FormData.fromMap({
      ...request.toFields(),
      'AdminPassword': request.adminPassword,
      if (logoFile != null) 'Logo': await MultipartFile.fromFile(logoFile.path),
    });

    final response = await apiClient.post('organizations', data: formData);

    if (!_isSuccess(response.statusCode)) _handleError(response);

    return OrganizationResponse.fromJson(response.data as Map<String, dynamic>);
  }

  Future<OrganizationResponse> getOrganization(String id) {
    return getById(id, fromJson: OrganizationResponse.fromJson);
  }

  /// Updates the organization using multipart/form-data so a logo file can
  /// be attached alongside the regular text fields.
  Future<OrganizationResponse> updateOrganization(
    String id,
    OrganizationUpdateRequest request, {
    File? logoFile,
    bool removeLogo = false,
  }) async {
    final formData = FormData.fromMap({
      ...request.toFields(),
      'RemoveLogo': removeLogo.toString(),
      if (logoFile != null) 'Logo': await MultipartFile.fromFile(logoFile.path),
    });

    final response = await apiClient.put('organizations/$id', data: formData);

    if (!_isSuccess(response.statusCode)) _handleError(response);

    return OrganizationResponse.fromJson(response.data as Map<String, dynamic>);
  }
}
