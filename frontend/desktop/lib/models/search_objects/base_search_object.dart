class BaseSearchObject {
  int? page;
  int? pageSize;
  String? fts;

  BaseSearchObject({
    this.page = 0,
    this.pageSize = 10,
    this.fts,
  }) {
    assert(page == null || page! >= 0, 'page must be >= 0');
    assert(pageSize == null || pageSize! > 0, 'pageSize must be > 0');
  }

  /// `dynamic` (not `String`) so subclasses can add list-valued filters
  /// (e.g. `OrganizationSearchObject.organizationIds`) — Dio serializes a
  /// `List<String>` query value as repeated keys (`Key=a&Key=b`) by default,
  /// which is exactly what the backend's array-bound query params expect.
  Map<String, dynamic> toQueryString() {
    final map = <String, dynamic>{};

    if (page != null) {
      map['Page'] = page.toString();
    }
    if (pageSize != null) {
      map['PageSize'] = pageSize.toString();
    }
    if (fts != null && fts!.isNotEmpty) {
      map['FTS'] = fts!;
    }

    return map;
  }
}
