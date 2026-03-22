class CategoryUpdateRequest {
  final String name;
  final String description;
  final bool isActive;
  final int displayOrder;

  const CategoryUpdateRequest({
    required this.name,
    this.description = '',
    this.isActive = true,
    this.displayOrder = 0,
  });

  Map<String, String> toFields() => {
        'Name': name,
        'Description': description,
        'IsActive': isActive.toString(),
        'DisplayOrder': displayOrder.toString(),
      };
}
