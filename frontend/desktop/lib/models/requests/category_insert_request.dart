class CategoryInsertRequest {
  final String name;
  final String description;
  final bool isActive;
  final int displayOrder;

  const CategoryInsertRequest({
    required this.name,
    this.description = '',
    this.isActive = true,
    this.displayOrder = 0,
  });

  // Plain JSON body now — Icon moved to the dedicated POST /categories/{id}/icon
  // endpoint, so this request is no longer sent as multipart/form-data and
  // values keep their real types (not stringified for form fields).
  Map<String, dynamic> toJson() => {
        'Name': name,
        'Description': description,
        'IsActive': isActive,
        'DisplayOrder': displayOrder,
      };
}
