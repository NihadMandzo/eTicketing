/// Profile-only edit of an existing staff account (SuperAdmin, Admin,
/// OrganizationSuperAdmin or OrganizationAdmin) — role and active status
/// aren't editable through this request. Backs PUT /api/admins/{id}.
class StaffUserUpdateRequest {
  final String firstName;
  final String lastName;
  final String email;
  final String username;
  final String? phoneNumber;

  const StaffUserUpdateRequest({
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
