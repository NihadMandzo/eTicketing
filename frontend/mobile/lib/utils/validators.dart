/// Shared form-field validators mirroring backend FluentValidation rules — see
/// .claude/rules/00-workflow-and-testing.md's "validators are two-sided" rule.
class Validators {
  Validators._();

  /// Mirrors the backend's shared `PasswordRules.Password()`
  /// (Identity/Shared/Validators/PasswordRules.cs) — 8-100 characters with at least one
  /// lowercase, one uppercase, one digit and one special character.
  ///
  /// "Special" is anything that is not a letter, a digit, or whitespace, defined by exclusion for
  /// the same reason it is on the backend: an allow-list silently rejects every punctuation mark
  /// nobody thought to list. Nothing here restricts which characters a password may contain; the
  /// rules only say what it must contain at least one of.
  ///
  /// This form previously required only a minimum length, so a password the API rejected got no
  /// feedback until the request came back.
  static final RegExp _lowercase = RegExp(r'[a-z]');
  static final RegExp _uppercase = RegExp(r'[A-Z]');
  static final RegExp _digit = RegExp(r'[0-9]');
  static final RegExp _special = RegExp(r'[^a-zA-Z0-9\s]');

  static const int passwordMinLength = 8;
  static const int passwordMaxLength = 100;

  /// Returns null when [value] is acceptable, or the first failing rule's Bosnian message. Order
  /// matches the backend's CascadeMode.Stop so both sides report the same reason.
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

  /// Mirrors PurchaseRequestValidator's SimulatedLast4 rule on the backend, applied after stripping
  /// the spaces the field is typed with. Mock provider only: with Stripe the card is typed into
  /// Stripe's own payment sheet and never reaches a form in this app.
  static String? cardNumber(String? value) {
    final digits = (value ?? '').replaceAll(RegExp(r'\s'), '');
    return RegExp(r'^\d{12,19}$').hasMatch(digits)
        ? null
        : 'Unesite ispravan broj kartice (12-19 cifara)';
  }

  static String? cardExpiry(String? value) =>
      RegExp(r'^(0[1-9]|1[0-2])\/\d{2}$').hasMatch(value ?? '') ? null : 'Format MM/GG';

  static String? cardCvv(String? value) =>
      RegExp(r'^\d{3,4}$').hasMatch(value ?? '') ? null : '3-4 cifre';
}
