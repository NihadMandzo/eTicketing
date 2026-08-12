import 'base_search_object.dart';

/// Adds the category-multiselect filter to the organizations list — see
/// [CategoryMultiSelectFilter]. [organizationIds] is pre-resolved by the
/// caller (via `EventOrganizationIdsProvider`) from the selected category
/// ids, since Organizations and Events/Categories live in separate
/// microservices/databases and can't be joined server-side here.
class OrganizationSearchObject extends BaseSearchObject {
  final List<String>? organizationIds;

  OrganizationSearchObject({
    super.page,
    super.pageSize,
    super.fts,
    this.organizationIds,
  });

  @override
  Map<String, dynamic> toQueryString() {
    final map = super.toQueryString();
    if (organizationIds != null) {
      map['OrganizationIds'] = organizationIds;
    }
    return map;
  }
}
