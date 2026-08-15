/// Mirrors the backend's `eTicketing.Identity.Data.Enums.RoleType` ordinals exactly
/// (SuperAdmin(1), Admin(2), OrganizationSuperAdmin(3), OrganizationAdmin(4), User(5) — see
/// .claude/rules/01-domain.md).
///
/// Only for the raw-numeric JSON-body serialization convention (System.Text.Json's default enum
/// (de)serialization, no JsonStringEnumConverter registered) — e.g. `AddOrganizationUserRequest`/
/// `OrganizationUserInsertRequest`'s `Role` field on the wire. This is the opposite convention
/// from query-string-bound enums (`[AsParameters]`, e.g. `AdminQuery.RoleFilters`), which accept
/// the enum member **name** as a string instead — don't conflate the two when adding a new
/// request that carries a role.
enum RoleType {
  superAdmin(1),
  admin(2),
  organizationSuperAdmin(3),
  organizationAdmin(4),
  user(5);

  final int value;
  const RoleType(this.value);
}
