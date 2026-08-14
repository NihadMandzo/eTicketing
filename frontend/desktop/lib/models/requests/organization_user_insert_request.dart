/// Adds a new OrganizationAdmin to an organization — used by an
/// OrganizationSuperAdmin self-servicing their own org's staff (POST
/// /api/organizations/{organizationId}/users). The role is always
/// OrganizationAdmin here: the backend independently enforces that a
/// self-servicing OrganizationSuperAdmin can only ever create that role (see
/// OrganizationService.AddUserAsync), so there is nothing for this screen to
/// let the caller choose.
///
/// Role is sent as the backend's raw eTicketing.Identity.Data.Enums.RoleType
/// numeric value (System.Text.Json's default enum (de)serialization, no
/// JsonStringEnumConverter registered) — 4 = OrganizationAdmin.
class OrganizationUserInsertRequest {
  final String firstName;
  final String lastName;
  final String email;
  final String username;
  final String password;
  final String? phoneNumber;

  const OrganizationUserInsertRequest({
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
        'Role': 4, // RoleType.OrganizationAdmin
        if (phoneNumber != null && phoneNumber!.isNotEmpty) 'PhoneNumber': phoneNumber,
      };
}
