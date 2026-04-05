class AdminUserResponse {
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
  final DateTime? lastLoginAt;
  final DateTime createdAt;

  const AdminUserResponse({
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
    this.lastLoginAt,
    required this.createdAt,
  });

  factory AdminUserResponse.fromJson(Map<String, dynamic> json) {
    return AdminUserResponse(
      id: json['id'] as int? ?? 0,
      firstName: json['firstName'] as String? ?? '',
      lastName: json['lastName'] as String? ?? '',
      username: json['username'] as String? ?? '',
      email: json['email'] as String? ?? '',
      phoneNumber: json['phoneNumber'] as String?,
      isActive: json['isActive'] as bool? ?? false,
      isEmailVerified: json['isEmailVerified'] as bool? ?? false,
      isFirstLogin: json['isFirstLogin'] as bool? ?? false,
      roleName: json['roleName'] as String? ?? '',
      lastLoginAt: json['lastLoginAt'] != null
          ? DateTime.tryParse(json['lastLoginAt'] as String)
          : null,
      createdAt: json['createdAt'] != null
          ? (DateTime.tryParse(json['createdAt'] as String) ?? DateTime.now())
          : DateTime.now(),
    );
  }

  String get fullName => '$firstName $lastName';
}
