/// Shared form-field validators mirroring backend FluentValidation rules — see
/// .claude/rules/00-workflow-and-testing.md's "validators are two-sided" rule. Extracted here
/// instead of duplicating inline in each dialog, so a future backend regex change only needs one
/// edit (see StaffProfileRequestValidator on the backend for the rule this mirrors).
class Validators {
  Validators._();

  static final RegExp _phoneRegex = RegExp(r'^\+?[0-9\s\-()]{6,20}$');

  /// Mirrors the backend's `Matches(@"^\+?[0-9\s\-()]{6,20}$")` rule applied to every optional
  /// PhoneNumber field (UpdateStaffUserRequestValidator, UpdateOrganizationUserRequestValidator —
  /// both now backed by the shared `StaffProfileRequestValidator<T>`). Phone is optional
  /// everywhere it's used, so an empty/null value is valid.
  static String? phoneNumber(String? value) {
    final trimmed = value?.trim() ?? '';
    if (trimmed.isEmpty) return null;
    if (!_phoneRegex.hasMatch(trimmed)) {
      return 'Broj telefona nije u ispravnom formatu';
    }
    return null;
  }
}
