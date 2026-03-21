class UpdateUserRequest {
  final String firstName;
  final String lastName;
  final String username;
  final String? phoneNumber;

  const UpdateUserRequest({
    required this.firstName,
    required this.lastName,
    required this.username,
    this.phoneNumber,
  });

  Map<String, dynamic> toJson() => {
        'firstName': firstName,
        'lastName': lastName,
        'username': username,
        'phoneNumber': phoneNumber,
      };
}
