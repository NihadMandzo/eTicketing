/// Body for POST /auth/set-new-password — the forced-password-change flow
/// (MustChangePassword was true, see ForceChangePasswordScreen). Unlike
/// ChangePasswordRequest this needs no currentPassword: the caller is
/// already authenticated with the SuperAdmin-assigned password.
class SetNewPasswordRequest {
  final String newPassword;
  final String confirmPassword;

  const SetNewPasswordRequest({required this.newPassword, required this.confirmPassword});

  Map<String, dynamic> toJson() => {
        'newPassword': newPassword,
        'confirmPassword': confirmPassword,
      };
}
