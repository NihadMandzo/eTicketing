/// Body for POST /admins/{id}/set-password — SuperAdmin directly sets a
/// staff/organization account's password (no current password needed,
/// unlike self-service ChangePasswordRequest).
class SetPasswordRequest {
  final String newPassword;
  final String confirmPassword;

  const SetPasswordRequest({required this.newPassword, required this.confirmPassword});

  Map<String, dynamic> toJson() => {
        'NewPassword': newPassword,
        'ConfirmPassword': confirmPassword,
      };
}
