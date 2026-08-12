import 'base_search_object.dart';

/// Adds a role filter to the organization-users query (GET
/// /organizations/{id}/users?Role=...) — used to tell an organization's
/// Admins and SuperAdmins apart on the organization detail screen.
/// [role] should be `'OrganizationAdmin'` or `'OrganizationSuperAdmin'`.
class OrganizationUserSearchObject extends BaseSearchObject {
  final String? role;

  OrganizationUserSearchObject({
    super.page,
    super.pageSize,
    super.fts,
    this.role,
  });

  @override
  Map<String, dynamic> toQueryString() {
    final map = super.toQueryString();
    if (role != null) {
      map['Role'] = role;
    }
    return map;
  }
}
