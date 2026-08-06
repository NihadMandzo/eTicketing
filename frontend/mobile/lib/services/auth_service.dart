import 'package:dio/dio.dart';

import '../core/api_client.dart';
import '../core/session.dart';
import '../models/api_error.dart';
import '../models/requests/login_request.dart';
import '../models/requests/register_request.dart';
import '../models/responses/login_response.dart';
import '../models/responses/user_response.dart';
import 'api_exception.dart';

/// All auth state lives server-side in the httpOnly session cookie — this
/// class never reads or stores a token itself. [apiClient]'s cookie jar
/// attaches/receives `eticketing_at`/`eticketing_rt` automatically. On
/// success, [Session.currentUser] is updated so the rest of the app can
/// react to sign-in/sign-out without re-fetching the profile.
class AuthService {
  static const String _endpoint = 'Auth';

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

  /// Self-registration — defaults to the "User"/buyer role on the backend.
  Future<UserResponse> register(RegisterRequest request) async {
    final response = await apiClient.post('$_endpoint/register', data: request.toJson());

    if (!_isSuccess(response.statusCode)) _handleError(response);

    final loginResponse = LoginResponse.fromJson(response.data as Map<String, dynamic>);
    Session.currentUser.value = loginResponse.user;
    return loginResponse.user;
  }

  Future<UserResponse> login(LoginRequest request) async {
    final response = await apiClient.post('$_endpoint/login', data: request.toJson());

    if (!_isSuccess(response.statusCode)) _handleError(response);

    final loginResponse = LoginResponse.fromJson(response.data as Map<String, dynamic>);
    Session.currentUser.value = loginResponse.user;
    return loginResponse.user;
  }

  /// Attempts to restore a session from a persisted cookie (app start) or
  /// silently renew an expired access token via the refresh cookie.
  Future<UserResponse?> me() async {
    final response = await apiClient.get('$_endpoint/me');

    if (!_isSuccess(response.statusCode)) {
      Session.currentUser.value = null;
      return null;
    }

    final user = UserResponse.fromJson(response.data as Map<String, dynamic>);
    Session.currentUser.value = user;
    return user;
  }

  Future<void> logout() async {
    try {
      await apiClient.post('$_endpoint/logout');
    } catch (_) {
      // Best-effort — the app still clears local state below regardless.
    } finally {
      Session.currentUser.value = null;
    }
  }
}
