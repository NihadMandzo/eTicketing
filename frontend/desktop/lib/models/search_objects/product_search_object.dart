import 'base_search_object.dart';

class ProductSearchObject extends BaseSearchObject {
  final int? categoryId;
  final int? status; // PublishStatus: 0 = Draft, 1 = Published

  ProductSearchObject({
    super.page,
    super.pageSize,
    super.fts,
    this.categoryId,
    this.status,
  });

  @override
  Map<String, dynamic> toQueryString() {
    final map = super.toQueryString();
    if (categoryId != null) map['CategoryId'] = categoryId.toString();
    if (status != null) map['Status'] = status.toString();
    return map;
  }
}
