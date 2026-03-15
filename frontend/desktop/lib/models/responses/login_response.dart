class LoginResponse {
  final String token;
  final bool isFirstLogin;
  final String message;

  const LoginResponse({
    required this.token,
    required this.isFirstLogin,
    required this.message,
  });

  factory LoginResponse.fromJson(Map<String, dynamic> json) {
    return LoginResponse(
      token: json['token'] as String? ?? '',
      isFirstLogin: json['isFirstLogin'] as bool? ?? false,
      message: json['message'] as String? ?? '',
    );
  }
}
