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

  @override
  String toString() {
    return 'LoginRequest(emailOrUsername: $emailOrUsername, password: ****)';
  }
}
