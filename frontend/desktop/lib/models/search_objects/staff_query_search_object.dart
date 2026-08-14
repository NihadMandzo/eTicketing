import 'base_search_object.dart';

/// Adds the platform-staff role filter to GET /api/admins — see the role-filter
/// multiselect on the SuperAdmin-facing Users screen. [roleFilters] is a list of
/// RoleType enum member names ('SuperAdmin', 'Admin', 'OrganizationSuperAdmin',
/// 'OrganizationAdmin') — Dio serializes a `List<String>` query value as
/// repeated keys (`RoleFilters=A&RoleFilters=B`), matching the backend's
/// array-bound query param binding (same convention as
/// OrganizationSearchObject.organizationIds). Null or empty means "every role"
/// (AdminService.GetAsync's default, no filter).
class StaffQuerySearchObject extends BaseSearchObject {
  final List<String>? roleFilters;

  StaffQuerySearchObject({
    super.page,
    super.pageSize,
    super.fts,
    this.roleFilters,
  });

  @override
  Map<String, dynamic> toQueryString() {
    final map = super.toQueryString();
    if (roleFilters != null && roleFilters!.isNotEmpty) {
      map['RoleFilters'] = roleFilters;
    }
    return map;
  }
}
