class PagedResult<T> {
  List<T> items;
  int totalCount;
  int? page;
  int? pageSize;

  PagedResult({
    required this.items,
    required this.totalCount,
    this.page,
    this.pageSize,
  });

  factory PagedResult.fromJson(
    Map<String, dynamic> json,
    T Function(Map<String, dynamic>) fromJsonT,
  ) {
    return PagedResult<T>(
      items: json['items'] != null
          ? (json['items'] as List)
              .map((item) => fromJsonT(item as Map<String, dynamic>))
              .toList()
          : <T>[],
      totalCount: json['totalCount'] ?? 0,
      page: json['page'],
      pageSize: json['pageSize'],
    );
  }
}
