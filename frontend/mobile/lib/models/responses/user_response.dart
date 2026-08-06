class UserResponse {
  final String id;
  final String firstName;
  final String lastName;
  final String email;
  final String username;
  final String? phoneNumber;
  final String roleName;
  final String? organizationId;
  final bool isActive;
  final bool isEmailVerified;
  final bool isFirstLogin;
  final DateTime createdAt;
  final DateTime? lastLoginAt;

  const UserResponse({
    required this.id,
    required this.firstName,
    required this.lastName,
    required this.email,
    required this.username,
    this.phoneNumber,
    required this.roleName,
    this.organizationId,
    required this.isActive,
    required this.isEmailVerified,
    required this.isFirstLogin,
    required this.createdAt,
    this.lastLoginAt,
  });

  factory UserResponse.fromJson(Map<String, dynamic> json) {
    return UserResponse(
      id: json['id'] as String,
      firstName: json['firstName'] as String,
      lastName: json['lastName'] as String,
      email: json['email'] as String,
      username: json['username'] as String,
      phoneNumber: json['phoneNumber'] as String?,
      roleName: json['roleName'] as String,
      organizationId: json['organizationId'] as String?,
      isActive: json['isActive'] as bool? ?? false,
      isEmailVerified: json['isEmailVerified'] as bool? ?? false,
      isFirstLogin: json['isFirstLogin'] as bool? ?? false,
      createdAt: DateTime.parse(json['createdAt'] as String),
      lastLoginAt: json['lastLoginAt'] != null ? DateTime.tryParse(json['lastLoginAt'] as String) : null,
    );
  }

  String get fullName => '$firstName $lastName';
}
