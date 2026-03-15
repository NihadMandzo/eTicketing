class BaseSearchObject {
  int? page;
  int? pageSize;
  String? fts;

  BaseSearchObject({
    this.page = 0,
    this.pageSize = 10,
    this.fts,
  });

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
