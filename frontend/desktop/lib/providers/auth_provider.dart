import 'dart:convert';

import 'package:http/http.dart' as http;

import '../models/api_error.dart';
import '../models/requests/change_password_request.dart';
import '../models/requests/login_request.dart';
import '../models/responses/login_response.dart';
import '../models/responses/user_profile.dart';
import 'api_exception.dart';
import 'authorization.dart';


class AuthProvider {
  static const String _baseUrl = 'http://localhost:5189/api/';
  static const String _endpoint = 'Auth';

  // ── Helpers ──────────────────────────────────────────────────────────────

  Map<String, String> _publicHeaders() => {
        'Content-Type': 'application/json',
      };

  Map<String, String> _authHeaders() => {
        'Content-Type': 'application/json',
        if (Authorization.token != null && Authorization.token!.isNotEmpty)
          'Authorization': 'Bearer ${Authorization.token}',
      };

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

  bool _isSuccess(int code) => code >= 200 && code < 300;

  // ── login ─────────────────────────────────────────────────────────────────
  /// Authenticates the user. On success, the JWT token is automatically
  /// stored in [Authorization.token] so every subsequent request carries it.
  Future<LoginResponse> login(LoginRequest request) async {
    final uri = Uri.parse('$_baseUrl$_endpoint/login');

    final response = await http.post(
      uri,
      headers: _publicHeaders(),
      body: jsonEncode(request.toJson()),
    ).timeout(_timeout);

    if (!_isSuccess(response.statusCode)) _handleError(response);

    final data = jsonDecode(response.body) as Map<String, dynamic>;
    final loginResponse = LoginResponse.fromJson(data);

    // 🔑 Register the token globally so BaseProvider sends it on every request
    Authorization.token = loginResponse.token;

    return loginResponse;
  }

  // ── me ────────────────────────────────────────────────────────────────────
  /// Returns the profile of the currently authenticated user.
  Future<UserProfile> me() async {
    final uri = Uri.parse('$_baseUrl$_endpoint/me');

    final response = await http.get(uri, headers: _authHeaders());

    if (!_isSuccess(response.statusCode)) _handleError(response);

    final data = jsonDecode(response.body) as Map<String, dynamic>;
    return UserProfile.fromJson(data);
  }

  // ── changePassword ────────────────────────────────────────────────────────
  /// Changes the password for the currently authenticated user.
  /// Returns [true] on success, throws [ApiException] on failure.
  Future<bool> changePassword(ChangePasswordRequest request) async {
    final uri = Uri.parse('$_baseUrl$_endpoint/change-password');

    final response = await http.post(
      uri,
      headers: _authHeaders(),
      body: jsonEncode(request.toJson()),
    );

    if (!_isSuccess(response.statusCode)) _handleError(response);

    return true;
  }
}
