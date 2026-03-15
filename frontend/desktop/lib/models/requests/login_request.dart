class LoginRequest {
  final String emailOrUsername;
  final String password;

  const LoginRequest({
    required this.emailOrUsername,
    required this.password,
  });

  Map<String, dynamic> toJson() => {
        'emailOrUsername': emailOrUsername,
        'password': password,
      };
}
