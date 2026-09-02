/// Creates a platform `Admin` account — SuperAdmin only (POST /api/admins).
///
/// There is deliberately no `Role` field, unlike [OrganizationUserInsertRequest]: the backend
/// pins it via Mapster (`AdminMappingConfig` maps `Role => RoleType.Admin` on the
/// CreateAdminRequest -> User config), so the role is not the client's to send and cannot be
/// spoofed by one. Shape mirrors `CreateAdminRequest` in AdminDtos.cs.
class AdminInsertRequest {
  final String firstName;
  final String lastName;
  final String email;
  final String username;
  final String password;
  final String? phoneNumber;

  const AdminInsertRequest({
    required this.firstName,
    required this.lastName,
    required this.email,
    required this.username,
    required this.password,
    this.phoneNumber,
  });

  Map<String, dynamic> toJson() => {
        'FirstName': firstName,
        'LastName': lastName,
        'Email': email,
        'Username': username,
        'Password': password,
        if (phoneNumber != null && phoneNumber!.isNotEmpty) 'PhoneNumber': phoneNumber,
      };
}
