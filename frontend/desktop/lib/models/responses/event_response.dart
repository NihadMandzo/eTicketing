class EventResponse {
  final String id;
  final String name;
  final String description;
  final DateTime date;
  final int categoryId;
  final String categoryName;
  final String organizationId;
  final int status; // 0 = Draft, 1 = Published (PublishStatus, serialized as a plain int)
  final String? imageUrl;
  final DateTime createdAt;

  const EventResponse({
    required this.id,
    required this.name,
    required this.description,
    required this.date,
    required this.categoryId,
    required this.categoryName,
    required this.organizationId,
    required this.status,
    this.imageUrl,
    required this.createdAt,
  });

  bool get isPublished => status == 1;

  factory EventResponse.fromJson(Map<String, dynamic> json) {
    if (json['id'] == null) throw const FormatException('Missing id in payload');
    if (json['name'] == null) throw const FormatException('Missing name in payload');
    if (json['date'] == null) throw const FormatException('Missing date in payload');
    if (json['organizationId'] == null) throw const FormatException('Missing organizationId in payload');
    if (json['createdAt'] == null) throw const FormatException('Missing createdAt in payload');

    return EventResponse(
      id: json['id'] as String,
      name: json['name'] as String,
      description: json['description'] as String? ?? '',
      date: DateTime.parse(json['date'] as String),
      categoryId: json['categoryId'] as int? ?? 0,
      categoryName: json['categoryName'] as String? ?? '',
      organizationId: json['organizationId'] as String,
      status: json['status'] as int? ?? 0,
      imageUrl: json['imageUrl'] as String?,
      createdAt: DateTime.parse(json['createdAt'] as String),
    );
  }
}
