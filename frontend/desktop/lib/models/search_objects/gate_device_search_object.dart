import 'base_search_object.dart';

class GateDeviceSearchObject extends BaseSearchObject {
  final String? productId;

  GateDeviceSearchObject({
    super.page,
    super.pageSize,
    super.fts,
    this.productId,
  });

  @override
  Map<String, dynamic> toQueryString() {
    final map = super.toQueryString();
    if (productId != null) map['ProductId'] = productId;
    return map;
  }
}
