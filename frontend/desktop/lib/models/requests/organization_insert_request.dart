class OrganizationInsertRequest {
  final String name;
  final String description;
  final String address;
  final String phoneNumber;
  final String email;
  final String? website;

  /// Manually typed by SuperAdmin — the recipient of the "organization
  /// created" notification email. Deliberately independent of [adminEmail]:
  /// the account being created (the org's first OrganizationSuperAdmin) is
  /// not necessarily who SuperAdmin wants to notify.
  final String notificationEmail;

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
    required this.notificationEmail,
    required this.adminFirstName,
    required this.adminLastName,
    required this.adminEmail,
    required this.adminUsername,
    required this.adminPassword,
    this.adminPhoneNumber,
  });

  // Plain JSON body now — Logo moved to the dedicated POST /organizations/{id}/logo
  // endpoint, so this request is no longer sent as multipart/form-data. There is no
  // AdminRole field at all — the backend's CreateOrganizationRequest -> User mapping
  // hardcodes the first account created alongside a new organization to
  // OrganizationSuperAdmin (every organization must have exactly one), so there's
  // nothing for the caller to choose here.
  Map<String, dynamic> toJson() => {
        'Name': name,
        'Description': description,
        'Address': address,
        'PhoneNumber': phoneNumber,
        'Email': email,
        'Website': (website == null || website!.isEmpty) ? null : website,
        'NotificationEmail': notificationEmail,
        'AdminFirstName': adminFirstName,
        'AdminLastName': adminLastName,
        'AdminEmail': adminEmail,
        'AdminUsername': adminUsername,
        'AdminPassword': adminPassword,
        if (adminPhoneNumber != null && adminPhoneNumber!.isNotEmpty)
          'AdminPhoneNumber': adminPhoneNumber,
      };
}
