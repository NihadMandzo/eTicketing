/// Same shape for create, update, and preview — mirrors the backend's UpsertSectorRequest.
class SectorUpsertRequest {
  final String productId;
  final String name;
  final int capacity;
  final double price;
  final int? periodYear;
  final int? periodMonth;

  const SectorUpsertRequest({
    required this.productId,
    required this.name,
    required this.capacity,
    required this.price,
    this.periodYear,
    this.periodMonth,
  });

  Map<String, dynamic> toJson() => {
        'ProductId': productId,
        'Name': name,
        'Capacity': capacity,
        'Price': price,
        'PeriodYear': periodYear,
        'PeriodMonth': periodMonth,
      };
}
