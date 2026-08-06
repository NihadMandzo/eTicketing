import 'package:dio/dio.dart';

import '../core/api_client.dart';
import '../models/api_error.dart';
import '../models/requests/change_password_request.dart';
import '../models/requests/login_request.dart';
import '../models/requests/update_user_request.dart';
import '../models/responses/login_response.dart';
import '../models/responses/user_profile.dart';
import 'api_exception.dart';

/// All auth state lives server-side in the httpOnly session cookie — this
/// class never reads or stores a token itself. [apiClient]'s cookie jar
/// attaches/receives `eticketing_at`/`eticketing_rt` automatically.
class AuthProvider {
  static const String _endpoint = 'Auth';

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

  bool _isSuccess(int? code) => code != null && code >= 200 && code < 300;

  /// Authenticates the user. On success, the backend writes the session
  /// cookies directly on the response — nothing for this app to store.
  Future<LoginResponse> login(LoginRequest request) async {
    final response = await apiClient.post('$_endpoint/login', data: request.toJson());

    if (!_isSuccess(response.statusCode)) _handleError(response);

    return LoginResponse.fromJson(response.data as Map<String, dynamic>);
  }

  /// Self-registration is intentionally not exposed here — the desktop
  /// console is admin/organizer-only; accounts are provisioned by a
  /// SuperAdmin via the Admins/Organizations screens instead.

  /// Silently renews the session using the refresh cookie. Returns the
  /// refreshed [LoginResponse], or throws [ApiException] if the refresh
  /// token is missing/expired/revoked (caller should route to the login screen).
  Future<LoginResponse> refresh() async {
    final response = await apiClient.post('$_endpoint/refresh');

    if (!_isSuccess(response.statusCode)) _handleError(response);

    return LoginResponse.fromJson(response.data as Map<String, dynamic>);
  }

  /// Revokes the current session server-side and clears the cookies.
  Future<void> logout() async {
    try {
      await apiClient.post('$_endpoint/logout');
    } catch (_) {
      // Logging out is best-effort from the UI's perspective — even if this
      // call fails (e.g. already-expired session), the caller still
      // navigates back to the login screen.
    }
  }

  /// Returns the profile of the currently authenticated user.
  Future<UserProfile> me() async {
    final response = await apiClient.get('$_endpoint/me');

    if (!_isSuccess(response.statusCode)) _handleError(response);

    return UserProfile.fromJson(response.data as Map<String, dynamic>);
  }

  /// Changes the password for the currently authenticated user.
  /// Returns [true] on success, throws [ApiException] on failure.
  Future<bool> changePassword(ChangePasswordRequest request) async {
    final response = await apiClient.post('$_endpoint/change-password', data: request.toJson());

    if (!_isSuccess(response.statusCode)) _handleError(response);

    return true;
  }

  /// Updates the profile of the currently authenticated user.
  /// Returns the updated [UserProfile] from the server response.
  Future<UserProfile> updateUser(UpdateUserRequest request) async {
    final response = await apiClient.put('$_endpoint/update-user', data: request.toJson());

    if (!_isSuccess(response.statusCode)) _handleError(response);

    return UserProfile.fromJson(response.data as Map<String, dynamic>);
  }
}
