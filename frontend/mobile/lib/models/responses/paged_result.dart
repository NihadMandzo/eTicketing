/// Mirrors `eTicketing.Contracts.Pagination.PagedResult<T>` — locked shape,
/// 0-indexed paging. See [[01-domain]]'s pagination contract note; do not
/// change field names or indexing here without also changing the backend.
class PagedResult<T> {
  final List<T> items;
  final int totalCount;
  final int page;
  final int pageSize;

  const PagedResult({
    required this.items,
    required this.totalCount,
    required this.page,
    required this.pageSize,
  });

  factory PagedResult.fromJson(Map<String, dynamic> json, T Function(Map<String, dynamic>) fromJsonT) {
    return PagedResult(
      items: (json['items'] as List? ?? []).map((e) => fromJsonT(e as Map<String, dynamic>)).toList(),
      totalCount: json['totalCount'] as int? ?? 0,
      page: json['page'] as int? ?? 0,
      pageSize: json['pageSize'] as int? ?? 0,
    );
  }
}
