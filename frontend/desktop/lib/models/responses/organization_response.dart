class OrganizationResponse {
  final String id;
  final String name;
  final String description;
  final String address;
  final String phoneNumber;
  final String email;
  final String? website;
  final String? logoUrl;
  final bool isActive;
  final int userCount;
  final DateTime createdAt;
  final DateTime? updatedAt;

  const OrganizationResponse({
    required this.id,
    required this.name,
    required this.description,
    required this.address,
    required this.phoneNumber,
    required this.email,
    this.website,
    this.logoUrl,
    required this.isActive,
    required this.userCount,
    required this.createdAt,
    this.updatedAt,
  });

  factory OrganizationResponse.fromJson(Map<String, dynamic> json) {
    if (json['id'] == null) throw const FormatException('Missing id');
    if (json['name'] == null) throw const FormatException('Missing name');
    if (json['address'] == null) throw const FormatException('Missing address');
    if (json['phoneNumber'] == null) throw const FormatException('Missing phoneNumber');
    if (json['email'] == null) throw const FormatException('Missing email');
    if (json['createdAt'] == null) throw const FormatException('Missing createdAt');

    return OrganizationResponse(
      id: json['id'] as String,
      name: json['name'] as String,
      description: json['description'] as String? ?? '',
      address: json['address'] as String,
      phoneNumber: json['phoneNumber'] as String,
      email: json['email'] as String,
      website: json['website'] as String?,
      logoUrl: json['logoUrl'] as String?,
      isActive: json['isActive'] as bool? ?? false,
      userCount: json['userCount'] as int? ?? 0,
      createdAt: DateTime.parse(json['createdAt'] as String),
      updatedAt: json['updatedAt'] != null
          ? DateTime.tryParse(json['updatedAt'] as String)
          : null,
    );
  }
}
