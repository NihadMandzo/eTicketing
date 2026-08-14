/// Profile-only edit of an existing OrganizationAdmin in an organization —
/// backs PUT /api/organizations/{organizationId}/users/{userId}. Role isn't
/// editable here, same reasoning as StaffUserUpdateRequest.
class OrganizationUserUpdateRequest {
  final String firstName;
  final String lastName;
  final String email;
  final String username;
  final String? phoneNumber;

  const OrganizationUserUpdateRequest({
    required this.firstName,
    required this.lastName,
    required this.email,
    required this.username,
    this.phoneNumber,
  });

  Map<String, dynamic> toJson() => {
        'FirstName': firstName,
        'LastName': lastName,
        'Email': email,
        'Username': username,
        if (phoneNumber != null && phoneNumber!.isNotEmpty) 'PhoneNumber': phoneNumber,
      };
}
