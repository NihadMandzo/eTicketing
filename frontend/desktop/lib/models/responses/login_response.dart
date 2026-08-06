import 'user_profile.dart';

/// The access/refresh tokens never appear here — the backend writes them
/// straight to httpOnly cookies. This mirrors the backend's actual
/// `LoginResponse { user }` shape.
class LoginResponse {
  final UserProfile user;

  const LoginResponse({required this.user});

  factory LoginResponse.fromJson(Map<String, dynamic> json) {
    if (json['user'] == null) {
      throw const FormatException('Missing user inside LoginResponse JSON');
    }
    return LoginResponse(
      user: UserProfile.fromJson(json['user'] as Map<String, dynamic>),
    );
  }
}
