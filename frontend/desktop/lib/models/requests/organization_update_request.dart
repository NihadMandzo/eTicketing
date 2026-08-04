class OrganizationUpdateRequest {
  final String name;
  final String description;
  final String address;
  final String phoneNumber;
  final String email;
  final String? website;
  final bool isActive;

  const OrganizationUpdateRequest({
    required this.name,
    required this.description,
    required this.address,
    required this.phoneNumber,
    required this.email,
    this.website,
    required this.isActive,
  });

  Map<String, String> toFields() => {
        'Name': name,
        'Description': description,
        'Address': address,
        'PhoneNumber': phoneNumber,
        'Email': email,
        if (website != null) 'Website': website!,
        'IsActive': isActive.toString(),
      };
}
