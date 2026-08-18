import 'base_search_object.dart';

class SectorSearchObject extends BaseSearchObject {
  final String? productId;
  final int? status; // PublishStatus: 0 = Draft, 1 = Published

  SectorSearchObject({
    super.page,
    super.pageSize,
    super.fts,
    this.productId,
    this.status,
  });

  @override
  Map<String, dynamic> toQueryString() {
    final map = super.toQueryString();
    if (productId != null) map['ProductId'] = productId;
    if (status != null) map['Status'] = status.toString();
    return map;
  }
}
