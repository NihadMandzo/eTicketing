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
        'AdminPassword': adminPassword,
        if (adminPhoneNumber != null && adminPhoneNumber!.isNotEmpty)
          'AdminPhoneNumber': adminPhoneNumber!,
      };
}
