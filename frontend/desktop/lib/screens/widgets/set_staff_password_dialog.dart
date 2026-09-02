import 'package:flutter/material.dart';
import 'package:lucide_icons_flutter/lucide_icons.dart';

import '../../models/requests/set_password_request.dart';
import '../../providers/user_provider.dart';
import '../../theme/app_colors.dart';
import '../../main.dart';
import '../../utils/validators.dart';

/// SuperAdmin directly sets a staff/organization account's password (no
/// current password needed — see AdminService.SetPasswordAsync on the
/// backend). Strength is checked by the shared [Validators.password], which mirrors the
/// backend's shared `PasswordRules` — the same rule every password field in the app now uses.
class SetStaffPasswordDialog extends StatefulWidget {
  final String userId;
  final String userFullName;
  final VoidCallback onSaved;

  const SetStaffPasswordDialog({
    super.key,
    required this.userId,
    required this.userFullName,
    required this.onSaved,
  });

  @override
  State<SetStaffPasswordDialog> createState() => _SetStaffPasswordDialogState();
}

class _SetStaffPasswordDialogState extends State<SetStaffPasswordDialog> {
  final _formKey = GlobalKey<FormState>();
  final _newPasswordCtrl = TextEditingController();
  final _confirmPasswordCtrl = TextEditingController();
  bool _obscurePassword = true;
  bool _isSaving = false;

  bool get _isDark => Theme.of(context).brightness == Brightness.dark;
  Color get _primary => _isDark ? AppColors.secondary : AppColors.primary;
  Color get _primaryDark => _isDark ? AppColors.primary : AppColors.primaryDark;

  @override
  void dispose() {
    _newPasswordCtrl.dispose();
    _confirmPasswordCtrl.dispose();
    super.dispose();
  }

  InputDecoration _inputDecoration(String hint, {IconData? prefixIcon, Widget? suffixIcon}) {
    final placeholderColor = _isDark ? AppColors.darkTextTertiary : AppColors.lightTextDisabled;
    final borderColor = _isDark ? AppColors.darkBorderInput : AppColors.lightBorderInput;
    return InputDecoration(
      hintText: hint,
      hintStyle: TextStyle(color: placeholderColor, fontSize: 13),
      contentPadding: const EdgeInsets.symmetric(horizontal: 14, vertical: 12),
      prefixIcon: prefixIcon != null ? Icon(prefixIcon, color: placeholderColor, size: 18) : null,
      suffixIcon: suffixIcon,
      border: OutlineInputBorder(borderRadius: BorderRadius.circular(12), borderSide: BorderSide(color: borderColor)),
      enabledBorder: OutlineInputBorder(borderRadius: BorderRadius.circular(12), borderSide: BorderSide(color: borderColor)),
      focusedBorder: OutlineInputBorder(borderRadius: BorderRadius.circular(12), borderSide: BorderSide(color: _primary, width: 2)),
      errorBorder: OutlineInputBorder(borderRadius: BorderRadius.circular(12), borderSide: const BorderSide(color: AppColors.error)),
      focusedErrorBorder: OutlineInputBorder(borderRadius: BorderRadius.circular(12), borderSide: const BorderSide(color: AppColors.error, width: 2)),
      filled: true,
      fillColor: _isDark ? AppColors.darkInputFill : AppColors.lightInputFill,
    );
  }

  Future<void> _submit() async {
    if (!_formKey.currentState!.validate()) return;
    setState(() => _isSaving = true);

    try {
      await AdminProvider().setPassword(
        widget.userId,
        SetPasswordRequest(
          newPassword: _newPasswordCtrl.text,
          confirmPassword: _confirmPasswordCtrl.text,
        ).toJson(),
      );

      if (mounted) {
        Navigator.of(context).pop();
        widget.onSaved();
        handleApiSuccess('Lozinka je uspješno promijenjena. Korisnik će morati postaviti novu lozinku prilikom sljedeće prijave.');
      }
    } catch (e) {
      if (mounted) {
        setState(() => _isSaving = false);
        handleApiError(e);
      }
    }
  }

  @override
  Widget build(BuildContext context) {
    final isDark = _isDark;
    final textPrimary = isDark ? AppColors.darkTextPrimary : AppColors.lightTextPrimary;
    final textSecondary = isDark ? AppColors.darkTextSecondary : AppColors.lightTextSecondary;
    final onPrimaryColor = isDark ? AppColors.darkBackground : Colors.white;

    return Dialog(
      backgroundColor: Colors.transparent,
      insetPadding: const EdgeInsets.symmetric(horizontal: 32, vertical: 24),
      child: ConstrainedBox(
        constraints: const BoxConstraints(maxWidth: 440),
        child: ClipRRect(
          borderRadius: BorderRadius.circular(18),
          child: Material(
            color: isDark ? AppColors.darkSurface : Colors.white,
            child: Column(
              mainAxisSize: MainAxisSize.min,
              children: [
                Container(
                  padding: const EdgeInsets.symmetric(horizontal: 24, vertical: 18),
                  decoration: BoxDecoration(gradient: LinearGradient(colors: [_primary, _primaryDark])),
                  child: Row(
                    children: [
                      Expanded(
                        child: Text(
                          'Promijeni Lozinku',
                          style: TextStyle(fontSize: 20, fontWeight: FontWeight.w700, color: onPrimaryColor),
                        ),
                      ),
                      InkWell(
                        borderRadius: BorderRadius.circular(8),
                        onTap: () => Navigator.of(context).pop(),
                        child: Padding(
                          padding: const EdgeInsets.all(6),
                          child: Icon(LucideIcons.x, color: onPrimaryColor, size: 20),
                        ),
                      ),
                    ],
                  ),
                ),
                Flexible(
                  child: SingleChildScrollView(
                    padding: const EdgeInsets.all(24),
                    child: Form(
                      key: _formKey,
                      child: Column(
                        crossAxisAlignment: CrossAxisAlignment.start,
                        children: [
                          Text(
                            'Postavljate novu lozinku za "${widget.userFullName}". Korisnik će morati postaviti vlastitu lozinku prilikom sljedeće prijave.',
                            style: TextStyle(fontSize: 13, color: textSecondary),
                          ),
                          const SizedBox(height: 20),
                          Text('Nova Lozinka *', style: TextStyle(fontSize: 13, fontWeight: FontWeight.w600, color: textSecondary)),
                          const SizedBox(height: 6),
                          TextFormField(
                            controller: _newPasswordCtrl,
                            obscureText: _obscurePassword,
                            decoration: _inputDecoration(
                              'Minimalno 8 karaktera',
                              prefixIcon: LucideIcons.lock,
                              suffixIcon: IconButton(
                                icon: Icon(_obscurePassword ? LucideIcons.eyeOff : LucideIcons.eye, size: 18),
                                onPressed: () => setState(() => _obscurePassword = !_obscurePassword),
                              ),
                            ),
                            style: TextStyle(fontSize: 14, color: textPrimary),
                            validator: Validators.password,
                          ),
                          const SizedBox(height: 14),
                          Text('Potvrdite Lozinku *', style: TextStyle(fontSize: 13, fontWeight: FontWeight.w600, color: textSecondary)),
                          const SizedBox(height: 6),
                          TextFormField(
                            controller: _confirmPasswordCtrl,
                            obscureText: _obscurePassword,
                            decoration: _inputDecoration('Ponovite lozinku', prefixIcon: LucideIcons.lock),
                            style: TextStyle(fontSize: 14, color: textPrimary),
                            validator: (v) {
                              if (v != _newPasswordCtrl.text) return 'Lozinke se ne podudaraju';
                              return null;
                            },
                          ),
                        ],
                      ),
                    ),
                  ),
                ),
                Container(
                  padding: const EdgeInsets.symmetric(horizontal: 24, vertical: 16),
                  decoration: BoxDecoration(
                    color: isDark ? AppColors.darkSurfaceSubtle : AppColors.lightSurfaceSubtle,
                    border: Border(top: BorderSide(color: isDark ? AppColors.darkBorder : AppColors.lightBorder)),
                  ),
                  child: Row(
                    mainAxisAlignment: MainAxisAlignment.end,
                    children: [
                      OutlinedButton(
                        onPressed: _isSaving ? null : () => Navigator.of(context).pop(),
                        style: OutlinedButton.styleFrom(
                          foregroundColor: textSecondary,
                          side: BorderSide(color: isDark ? AppColors.darkBorderInput : AppColors.lightBorderInput),
                          shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(12)),
                          padding: const EdgeInsets.symmetric(horizontal: 24, vertical: 12),
                        ),
                        child: const Text('Otkaži', style: TextStyle(fontWeight: FontWeight.w500, fontSize: 14)),
                      ),
                      const SizedBox(width: 12),
                      Container(
                        decoration: BoxDecoration(
                          gradient: LinearGradient(colors: [_primary, _primaryDark]),
                          borderRadius: BorderRadius.circular(12),
                        ),
                        child: Material(
                          color: Colors.transparent,
                          child: InkWell(
                            borderRadius: BorderRadius.circular(12),
                            onTap: _isSaving ? null : _submit,
                            child: Padding(
                              padding: const EdgeInsets.symmetric(horizontal: 24, vertical: 12),
                              child: _isSaving
                                  ? SizedBox(
                                      width: 18,
                                      height: 18,
                                      child: CircularProgressIndicator(strokeWidth: 2, color: onPrimaryColor),
                                    )
                                  : Text(
                                      'Postavi Lozinku',
                                      style: TextStyle(color: onPrimaryColor, fontWeight: FontWeight.w600, fontSize: 14),
                                    ),
                            ),
                          ),
                        ),
                      ),
                    ],
                  ),
                ),
              ],
            ),
          ),
        ),
      ),
    );
  }
}
