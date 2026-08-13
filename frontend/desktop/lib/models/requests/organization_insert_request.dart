class OrganizationInsertRequest {
  final String name;
  final String description;
  final String address;
  final String phoneNumber;
  final String email;
  final String? website;
  final String adminFirstName;
  final String adminLastName;
  final String adminEmail;
  final String adminUsername;
  final String adminPassword;
  final String? adminPhoneNumber;

  const OrganizationInsertRequest({
    required this.name,
    required this.description,
    required this.address,
    required this.phoneNumber,
    required this.email,
    this.website,
    required this.adminFirstName,
    required this.adminLastName,
    required this.adminEmail,
    required this.adminUsername,
    required this.adminPassword,
    this.adminPhoneNumber,
  });

  // Plain JSON body now — Logo moved to the dedicated POST /organizations/{id}/logo
  // endpoint, so this request is no longer sent as multipart/form-data. AdminRole
  // is deliberately omitted: the backend's CreateOrganizationRequest.AdminRole
  // record property keeps its OrganizationSuperAdmin init default when the JSON
  // body doesn't include the key at all — "the first organizer created alongside
  // a new organization" is always an org super admin.
  Map<String, dynamic> toJson() => {
        'Name': name,
        'Description': description,
        'Address': address,
        'PhoneNumber': phoneNumber,
        'Email': email,
        'Website': (website == null || website!.isEmpty) ? null : website,
        'AdminFirstName': adminFirstName,
        'AdminLastName': adminLastName,
        'AdminEmail': adminEmail,
        'AdminUsername': adminUsername,
        'AdminPassword': adminPassword,
        if (adminPhoneNumber != null && adminPhoneNumber!.isNotEmpty)
          'AdminPhoneNumber': adminPhoneNumber,
      };
}
