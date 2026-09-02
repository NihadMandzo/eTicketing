import 'dart:typed_data';

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

  /// POST /api/organizations — plain JSON, metadata only. The logo (if any) is
  /// uploaded separately afterwards via [createLogo].
  Future<OrganizationResponse> insertOrganization(OrganizationInsertRequest request) async {
    final response = await send(() => apiClient.post('organizations', data: request.toJson()));

    if (!_isSuccess(response.statusCode)) _handleError(response);

    return OrganizationResponse.fromJson(response.data as Map<String, dynamic>);
  }

  Future<OrganizationResponse> getOrganization(String id) {
    return getById(id, fromJson: OrganizationResponse.fromJson);
  }

  /// PUT /api/organizations/:id — plain JSON, metadata only. Never touches the logo.
  Future<OrganizationResponse> updateOrganization(String id, OrganizationUpdateRequest request) async {
    final response = await send(() => apiClient.put('organizations/$id', data: request.toJson()));

    if (!_isSuccess(response.statusCode)) _handleError(response);

    return OrganizationResponse.fromJson(response.data as Map<String, dynamic>);
  }

  /// POST /api/organizations/:id/logo (multipart) — first-time logo upload. Fails
  /// with a conflict if the organization already has one; use [replaceLogo] then.
  Future<OrganizationResponse> createLogo(String id, Uint8List logoBytes) async {
    final formData = FormData.fromMap({
      'Logo': MultipartFile.fromBytes(logoBytes,
          filename: 'logo.${_extensionFor(logoBytes)}', contentType: _contentTypeFor(logoBytes)),
    });

    final response = await send(() => apiClient.post('organizations/$id/logo', data: formData));

    if (!_isSuccess(response.statusCode)) _handleError(response);

    return OrganizationResponse.fromJson(response.data as Map<String, dynamic>);
  }

  /// PUT /api/organizations/:id/logo (multipart) — replaces an existing logo in place.
  Future<OrganizationResponse> replaceLogo(String id, Uint8List logoBytes) async {
    final formData = FormData.fromMap({
      'Logo': MultipartFile.fromBytes(logoBytes,
          filename: 'logo.${_extensionFor(logoBytes)}', contentType: _contentTypeFor(logoBytes)),
    });

    final response = await send(() => apiClient.put('organizations/$id/logo', data: formData));

    if (!_isSuccess(response.statusCode)) _handleError(response);

    return OrganizationResponse.fromJson(response.data as Map<String, dynamic>);
  }

  /// Organization logos allow PNG or JPEG (unlike category icons, PNG-only). The cropped bytes
  /// no longer carry a file path/extension (ImageCropDialog hands back raw pixels, not a File),
  /// so the format is sniffed from the actual PNG magic number instead — mirrors the backend's
  /// own approach of never trusting client-supplied metadata
  /// (OrganizationLogoValidation.HasAllowedFormatAsync).
  bool _isPng(Uint8List bytes) =>
      bytes.length >= 4 && bytes[0] == 0x89 && bytes[1] == 0x50 && bytes[2] == 0x4E && bytes[3] == 0x47;

  DioMediaType _contentTypeFor(Uint8List bytes) =>
      _isPng(bytes) ? DioMediaType('image', 'png') : DioMediaType('image', 'jpeg');

  String _extensionFor(Uint8List bytes) => _isPng(bytes) ? 'png' : 'jpg';
}
