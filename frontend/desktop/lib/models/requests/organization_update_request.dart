class OrganizationUpdateRequest {
  final String name;
  final String description;
  final String address;
  final String phoneNumber;
  final String email;
  final String? website;
  final String? logoUrl;
  final bool isActive;

  const OrganizationUpdateRequest({
    required this.name,
    required this.description,
    required this.address,
    required this.phoneNumber,
    required this.email,
    this.website,
    this.logoUrl,
    required this.isActive,
  });

  Map<String, dynamic> toJson() => {
        'name': name,
        'description': description,
        'address': address,
        'phoneNumber': phoneNumber,
        'email': email,
        'website': website,
        'logoUrl': logoUrl,
        'isActive': isActive,
      };
}
