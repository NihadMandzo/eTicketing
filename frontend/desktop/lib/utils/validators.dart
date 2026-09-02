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
  /// Mirrors the backend's shared `PasswordRules.Password()` (Identity's
  /// Shared/Validators/PasswordRules.cs) — 8-100 characters with at least one lowercase, one
  /// uppercase, one digit and one special character.
  ///
  /// "Special" is anything that is not a letter, a digit, or whitespace, defined by exclusion for
  /// the same reason it is on the backend: the previous rule listed an allowed set
  /// (`[A-Za-z\d@$!%*?&#]`) and therefore silently *rejected* every punctuation mark nobody
  /// thought to list — `.` `,` `;` `'` `/` `[` `]` `\` `?` `_` `-` `=` `+` were all refused by the
  /// form even though the API would have accepted them.
  ///
  /// Nothing restricts which characters a password may contain; these rules only say what it must
  /// contain at least one of.
  static final RegExp _lowercase = RegExp(r'[a-z]');
  static final RegExp _uppercase = RegExp(r'[A-Z]');
  static final RegExp _digit = RegExp(r'[0-9]');
  static final RegExp _special = RegExp(r'[^a-zA-Z0-9\s]');

  static const int passwordMinLength = 8;
  static const int passwordMaxLength = 100;

  /// Returns null when [value] is an acceptable password, or the first failing rule's Bosnian
  /// message. Order matches the backend's CascadeMode.Stop so both sides report the same reason.
  static String? password(String? value) {
    final v = value ?? '';
    if (v.isEmpty) return 'Lozinka je obavezna';
    if (v.length < passwordMinLength) {
      return 'Lozinka mora imati najmanje $passwordMinLength karaktera';
    }
    if (v.length > passwordMaxLength) {
      return 'Lozinka može imati najviše $passwordMaxLength karaktera';
    }
    if (!_lowercase.hasMatch(v)) return 'Lozinka mora sadržavati barem jedno malo slovo';
    if (!_uppercase.hasMatch(v)) return 'Lozinka mora sadržavati barem jedno veliko slovo';
    if (!_digit.hasMatch(v)) return 'Lozinka mora sadržavati barem jednu cifru';
    if (!_special.hasMatch(v)) {
      return "Lozinka mora sadržavati barem jedan specijalni znak (npr. . , ; ' / [ ] ? _ - = + ! @ # \$ % & *)";
    }
    return null;
  }

  static String? phoneNumber(String? value) {
    final trimmed = value?.trim() ?? '';
    if (trimmed.isEmpty) return null;
    if (!_phoneRegex.hasMatch(trimmed)) {
      return 'Broj telefona nije u ispravnom formatu';
    }
    return null;
  }
}
