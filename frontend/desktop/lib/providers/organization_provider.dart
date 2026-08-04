import 'dart:async';
import 'dart:convert';
import 'dart:io';

import 'package:http/http.dart' as http;

import '../models/api_error.dart';
import '../models/requests/organization_insert_request.dart';
import '../models/requests/organization_update_request.dart';
import '../models/responses/organization_response.dart';
import 'api_exception.dart';
import 'authorization.dart';
import 'base_provider.dart';

class OrganizationProvider extends BaseProvider<OrganizationResponse> {
  OrganizationProvider() : super('Organizations');

  Map<String, String> _authHeaders() {
    if (Authorization.token != null && Authorization.token!.isNotEmpty) {
      return {'Authorization': 'Bearer ${Authorization.token}'};
    }
    return {};
  }

  /// POST /api/Organizations (multipart – logo is optional)
  Future<OrganizationResponse> insertOrganization(
    OrganizationInsertRequest request, {
    File? logoFile,
  }) async {
    final uri = Uri.parse('${BaseProvider.baseUrl}Organizations');
    final multipart = http.MultipartRequest('POST', uri)
      ..headers.addAll(_authHeaders())
      ..fields.addAll(request.toFields());

    if (logoFile != null) {
      multipart.files.add(
        await http.MultipartFile.fromPath('Logo', logoFile.path),
      );
    }

    http.Response response;
    try {
      response = await Future.timeout(
        const Duration(seconds: 15),
        () async {
          final streamed = await multipart.send();
          return await http.Response.fromStream(streamed);
        }(),
      );
    } on TimeoutException {
      throw ApiException(
        statusCode: 408,
        apiError: ApiError(displayMessage: 'Zahtjev je istekao (timeout).'),
      );
    }

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
    final uri = Uri.parse('${BaseProvider.baseUrl}Organizations/$id');

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

    http.Response response;
    try {
      response = await Future.timeout(
        const Duration(seconds: 15),
        () async {
          final streamed = await multipart.send();
          return await http.Response.fromStream(streamed);
        }(),
      );
    } on TimeoutException {
      throw ApiException(
        statusCode: 408,
        apiError: ApiError(displayMessage: 'Zahtjev je istekao (timeout).'),
      );
    }

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
