import '../enums/city.dart';
import 'base_search_object.dart';

class ProductSearchObject extends BaseSearchObject {
  final int? categoryId;
  final int? status; // PublishStatus: 0 = Draft, 1 = Published
  final City? city;

  ProductSearchObject({
    super.page,
    super.pageSize,
    super.fts,
    this.categoryId,
    this.status,
    this.city,
  });

  @override
  Map<String, dynamic> toQueryString() {
    final map = super.toQueryString();
    if (categoryId != null) map['CategoryId'] = categoryId.toString();
    if (status != null) map['Status'] = status.toString();
    if (city != null) map['City'] = city!.value.toString();
    return map;
  }
}
