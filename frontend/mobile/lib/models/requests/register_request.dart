class RegisterRequest {
  final String firstName;
  final String lastName;
  final String email;
  final String username;
  final String password;
  final String? phoneNumber;

  const RegisterRequest({
    required this.firstName,
    required this.lastName,
    required this.email,
    required this.username,
    required this.password,
    this.phoneNumber,
  });

  Map<String, dynamic> toJson() => {
        'firstName': firstName,
        'lastName': lastName,
        'email': email,
        'username': username,
        'password': password,
        if (phoneNumber != null && phoneNumber!.isNotEmpty) 'phoneNumber': phoneNumber,
      };
}
