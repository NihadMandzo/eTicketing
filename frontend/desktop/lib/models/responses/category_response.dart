class CategoryResponse {
  final int id;
  final String name;
  final String description;
  final String? iconUrl;
  final bool isActive;
  final int displayOrder;
  final DateTime createdAt;
  final DateTime? updatedAt;

  CategoryResponse({
    required this.id,
    required this.name,
    required this.description,
    required this.iconUrl,
    required this.isActive,
    required this.displayOrder,
    required this.createdAt,
    this.updatedAt,
  });

  factory CategoryResponse.fromJson(Map<String, dynamic> json) {
    if (json['id'] == null) throw const FormatException('Missing id in payload');
    if (json['name'] == null) throw const FormatException('Missing name in payload');
    // iconUrl is null until an icon has been uploaded via the dedicated icon endpoints — no
    // longer required on the payload (categories can now exist without one).

    DateTime? parsedCreatedAt;
    if (json['createdAt'] != null) {
      parsedCreatedAt = DateTime.tryParse(json['createdAt'] as String);
    }
    if (parsedCreatedAt == null) throw const FormatException('Missing or invalid createdAt in payload');

    return CategoryResponse(
      id: json['id'] as int,
      name: json['name'] as String,
      description: json['description'] as String? ?? '',
      iconUrl: json['iconUrl'] as String?,
      isActive: json['isActive'] as bool? ?? false,
      displayOrder: json['displayOrder'] as int? ?? 0,
      createdAt: parsedCreatedAt,
      updatedAt: json['updatedAt'] != null 
          ? DateTime.parse(json['updatedAt'] as String) 
          : null,
    );
  }

  Map<String, dynamic> toJson() {
    return {
      'id': id,
      'name': name,
      'description': description,
      'iconUrl': iconUrl,
      'isActive': isActive,
      'displayOrder': displayOrder,
      'createdAt': createdAt.toIso8601String(),
      'updatedAt': updatedAt?.toIso8601String(),
    };
  }
}
