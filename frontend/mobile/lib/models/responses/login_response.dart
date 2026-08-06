import 'user_response.dart';

/// The access/refresh tokens never appear here — the backend writes them
/// straight to httpOnly cookies. This mirrors the backend's actual
/// `LoginResponse { user }` shape.
class LoginResponse {
  final UserResponse user;

  const LoginResponse({required this.user});

  factory LoginResponse.fromJson(Map<String, dynamic> json) {
    return LoginResponse(user: UserResponse.fromJson(json['user'] as Map<String, dynamic>));
  }
}
