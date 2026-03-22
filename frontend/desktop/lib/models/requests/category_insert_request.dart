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

  Map<String, String> toFields() => {
        'Name': name,
        'Description': description,
        'IsActive': isActive.toString(),
        'DisplayOrder': displayOrder.toString(),
      };
}
