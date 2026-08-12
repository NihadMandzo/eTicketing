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

  Map<String, String> toFields() => {
        'Name': name,
        'Description': description,
        'Address': address,
        'PhoneNumber': phoneNumber,
        'Email': email,
        if (website != null && website!.isNotEmpty) 'Website': website!,
        'AdminFirstName': adminFirstName,
        'AdminLastName': adminLastName,
        'AdminEmail': adminEmail,
        'AdminUsername': adminUsername,
        if (adminPhoneNumber != null && adminPhoneNumber!.isNotEmpty)
          'AdminPhoneNumber': adminPhoneNumber!,
        // Must be sent explicitly now that this request is posted as
        // multipart/form-data (needed for the optional Logo upload) rather
        // than JSON — unlike JSON body binding, ASP.NET Core's [FromForm]
        // complex-type binder resets an absent field to its CLR default
        // (0, an invalid RoleType) instead of leaving the backend record's
        // AdminRole = RoleType.OrganizationSuperAdmin init default in
        // place. "The first organizer created alongside a new
        // organization" is always an org super admin.
        'AdminRole': 'OrganizationSuperAdmin',
      };

  Map<String, String> toAuthFields() => {
        ...toFields(),
        'AdminPassword': adminPassword,
      };
}
