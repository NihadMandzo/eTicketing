class LoginResponse {
  final String token;
  final bool? isFirstLogin;
  final String message;

  const LoginResponse({
    required this.token,
    this.isFirstLogin,
    required this.message,
  });

  factory LoginResponse.fromJson(Map<String, dynamic> json) {
    if (json['token'] == null) {
      throw const FormatException('Missing token inside LoginResponse JSON');
    }
    return LoginResponse(
      token: json['token'] as String,
      isFirstLogin: json['isFirstLogin'] as bool?,
      message: json['message'] as String? ?? '',
    );
  }
}
