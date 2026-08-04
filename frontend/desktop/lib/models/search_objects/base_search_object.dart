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

  Map<String, String> toQueryString() {
    final map = <String, String>{};

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
