class UserProfile {
  final int id;
  final String firstName;
  final String lastName;
  final String username;
  final String email;
  final String? phoneNumber;
  final bool isActive;
  final bool isEmailVerified;
  final bool isFirstLogin;
  final String roleName;
  final int? organizationId;
  final String? organizationName;
  final DateTime? lastLoginAt;
  final DateTime createdAt;

  const UserProfile({
    required this.id,
    required this.firstName,
    required this.lastName,
    required this.username,
    required this.email,
    this.phoneNumber,
    required this.isActive,
    required this.isEmailVerified,
    required this.isFirstLogin,
    required this.roleName,
    this.organizationId,
    this.organizationName,
    this.lastLoginAt,
    required this.createdAt,
  });

  factory UserProfile.fromJson(Map<String, dynamic> json) {
    if (json['id'] == null) throw const FormatException('Missing id');
    if (json['firstName'] == null) throw const FormatException('Missing firstName');
    if (json['lastName'] == null) throw const FormatException('Missing lastName');
    if (json['username'] == null) throw const FormatException('Missing username');
    if (json['email'] == null) throw const FormatException('Missing email');
    if (json['roleName'] == null) throw const FormatException('Missing roleName');
    if (json['createdAt'] == null) throw const FormatException('Missing createdAt');

    return UserProfile(
      id: json['id'] as int,
      firstName: json['firstName'] as String,
      lastName: json['lastName'] as String,
      username: json['username'] as String,
      email: json['email'] as String,
      phoneNumber: json['phoneNumber'] as String?,
      isActive: json['isActive'] as bool? ?? false,
      isEmailVerified: json['isEmailVerified'] as bool? ?? false,
      isFirstLogin: json['isFirstLogin'] as bool? ?? false,
      roleName: json['roleName'] as String,
      organizationId: json['organizationId'] as int?,
      organizationName: json['organizationName'] as String?,
      lastLoginAt: json['lastLoginAt'] != null
          ? DateTime.tryParse(json['lastLoginAt'] as String)
          : null,
      createdAt: DateTime.parse(json['createdAt'] as String),
    );
  }

  String get fullName => '$firstName $lastName';
}
