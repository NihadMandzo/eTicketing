import 'dart:convert';
import 'dart:io';

import 'package:http/http.dart' as http;

import '../models/api_error.dart';
import '../models/requests/organization_update_request.dart';
import '../models/responses/organization_response.dart';
import 'api_exception.dart';
import 'authorization.dart';
import 'base_provider.dart';

class OrganizationProvider extends BaseProvider<OrganizationResponse> {
  OrganizationProvider() : super('Organizations');

  static const String _baseUrl = 'http://localhost:5189/api/';

  Future<OrganizationResponse> getOrganization(int id) {
    return getById(id, fromJson: OrganizationResponse.fromJson);
  }

  /// Updates organization using multipart/form-data so a logo file can be
  /// attached alongside the regular text fields.
  Future<OrganizationResponse> updateOrganization(
    int id,
    OrganizationUpdateRequest request, {
    File? logoFile,
    bool removeLogo = false,
  }) async {
    final uri = Uri.parse('${_baseUrl}Organizations/$id');

    final multipart = http.MultipartRequest('PUT', uri);

    // Auth header
    if (Authorization.token != null && Authorization.token!.isNotEmpty) {
      multipart.headers['Authorization'] = 'Bearer ${Authorization.token}';
    }

    // Text fields
    multipart.fields.addAll(request.toFields());
    multipart.fields['RemoveLogo'] = removeLogo.toString();

    // Logo file (if provided)
    if (logoFile != null) {
      multipart.files.add(
        await http.MultipartFile.fromPath('Logo', logoFile.path),
      );
    }

    final streamed = await multipart.send();
    final response = await http.Response.fromStream(streamed);

    if (response.statusCode < 200 || response.statusCode >= 300) {
      ApiError apiError;
      try {
        final body = jsonDecode(response.body) as Map<String, dynamic>;
        apiError = ApiError.fromJson(body);
      } catch (_) {
        apiError = ApiError(errorCode: response.body);
      }
      throw ApiException(statusCode: response.statusCode, apiError: apiError);
    }

    final data = jsonDecode(response.body) as Map<String, dynamic>;
    return OrganizationResponse.fromJson(data);
  }
}
