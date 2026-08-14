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

  // Plain JSON body now — Logo/RemoveLogo moved to the dedicated
  // PUT /organizations/{id}/logo endpoint (logos can only be replaced, never
  // cleared without deleting the organization), so this request is no longer
  // sent as multipart/form-data.
  Map<String, dynamic> toJson() => {
        'Name': name,
        'Description': description,
        'Address': address,
        'PhoneNumber': phoneNumber,
        'Email': email,
        'Website': (website == null || website!.isEmpty) ? null : website,
        'IsActive': isActive,
      };
}
