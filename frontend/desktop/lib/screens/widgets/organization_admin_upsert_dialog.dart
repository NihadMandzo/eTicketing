import 'package:flutter/material.dart';
import 'package:lucide_icons_flutter/lucide_icons.dart';

import '../../main.dart';
import '../../models/requests/organization_user_insert_request.dart';
import '../../models/requests/organization_user_update_request.dart';
import '../../models/responses/admin_user_response.dart';
import '../../providers/user_provider.dart';
import '../../theme/app_colors.dart';
import '../../utils/validators.dart';

/// Add/edit dialog for an OrganizationSuperAdmin self-servicing OrganizationAdmin
/// accounts in their own organization (org Users tab). Create mode collects a
/// password (POST /organizations/{organizationId}/users); edit mode omits it
/// (PUT /organizations/{organizationId}/users/{userId}) — mirrors
/// organization_upsert_dialog.dart's admin-section field/validator conventions.
class OrganizationAdminUpsertDialog extends StatefulWidget {
  final String organizationId;
  final AdminUserResponse? user;
  final VoidCallback onSaved;

  const OrganizationAdminUpsertDialog({
    super.key,
    required this.organizationId,
    this.user,
    required this.onSaved,
  });

  @override
  State<OrganizationAdminUpsertDialog> createState() => _OrganizationAdminUpsertDialogState();
}

class _OrganizationAdminUpsertDialogState extends State<OrganizationAdminUpsertDialog> {
  final _formKey = GlobalKey<FormState>();
  final _provider = OrganizationUsersProvider();

  late final _firstNameCtrl = TextEditingController(text: widget.user?.firstName ?? '');
  late final _lastNameCtrl = TextEditingController(text: widget.user?.lastName ?? '');
  late final _emailCtrl = TextEditingController(text: widget.user?.email ?? '');
  late final _usernameCtrl = TextEditingController(text: widget.user?.username ?? '');
  late final _phoneCtrl = TextEditingController(text: widget.user?.phoneNumber ?? '');
  final _passwordCtrl = TextEditingController();

  bool _isSaving = false;
  bool _obscurePassword = true;

  bool get _isEditing => widget.user != null;
  bool get _isDark => Theme.of(context).brightness == Brightness.dark;
  Color get _primary => _isDark ? AppColors.secondary : AppColors.primary;
  Color get _primaryDark => _isDark ? AppColors.primary : AppColors.primaryDark;

  @override
  void dispose() {
    _firstNameCtrl.dispose();
    _lastNameCtrl.dispose();
    _emailCtrl.dispose();
    _usernameCtrl.dispose();
    _phoneCtrl.dispose();
    _passwordCtrl.dispose();
    super.dispose();
  }

  Future<void> _submit() async {
    if (!_formKey.currentState!.validate()) return;

    setState(() => _isSaving = true);
    try {
      if (_isEditing) {
        await _provider.update(
          widget.organizationId,
          widget.user!.id,
          OrganizationUserUpdateRequest(
            firstName: _firstNameCtrl.text.trim(),
            lastName: _lastNameCtrl.text.trim(),
            email: _emailCtrl.text.trim(),
            username: _usernameCtrl.text.trim(),
            phoneNumber: _phoneCtrl.text.trim().isEmpty ? null : _phoneCtrl.text.trim(),
          ),
          fromJson: AdminUserResponse.fromJson,
        );
      } else {
        await _provider.insert(
          widget.organizationId,
          OrganizationUserInsertRequest(
            firstName: _firstNameCtrl.text.trim(),
            lastName: _lastNameCtrl.text.trim(),
            email: _emailCtrl.text.trim(),
            username: _usernameCtrl.text.trim(),
            password: _passwordCtrl.text,
            phoneNumber: _phoneCtrl.text.trim().isEmpty ? null : _phoneCtrl.text.trim(),
          ),
          fromJson: AdminUserResponse.fromJson,
        );
      }

      if (mounted) {
        Navigator.of(context).pop();
        widget.onSaved();
      }
    } catch (e) {
      if (mounted) {
        setState(() => _isSaving = false);
        handleApiError(e);
      }
    }
  }

  InputDecoration _inputDecoration(String hint, {IconData? prefixIcon}) {
    final placeholderColor = _isDark ? AppColors.darkTextTertiary : AppColors.lightTextDisabled;
    final borderColor = _isDark ? AppColors.darkBorderInput : AppColors.lightBorderInput;
    return InputDecoration(
      hintText: hint,
      hintStyle: TextStyle(color: placeholderColor, fontSize: 13),
      contentPadding: const EdgeInsets.symmetric(horizontal: 14, vertical: 12),
      prefixIcon: prefixIcon != null ? Icon(prefixIcon, color: placeholderColor, size: 18) : null,
      border: OutlineInputBorder(borderRadius: BorderRadius.circular(12), borderSide: BorderSide(color: borderColor)),
      enabledBorder: OutlineInputBorder(borderRadius: BorderRadius.circular(12), borderSide: BorderSide(color: borderColor)),
      focusedBorder: OutlineInputBorder(borderRadius: BorderRadius.circular(12), borderSide: BorderSide(color: _primary, width: 2)),
      errorBorder: OutlineInputBorder(borderRadius: BorderRadius.circular(12), borderSide: const BorderSide(color: AppColors.error)),
      focusedErrorBorder: OutlineInputBorder(borderRadius: BorderRadius.circular(12), borderSide: const BorderSide(color: AppColors.error, width: 2)),
      filled: true,
      fillColor: _isDark ? AppColors.darkInputFill : AppColors.lightInputFill,
    );
  }

  @override
  Widget build(BuildContext context) {
    final isDark = _isDark;
    final textPrimary = isDark ? AppColors.darkTextPrimary : AppColors.lightTextPrimary;
    final onPrimaryColor = isDark ? AppColors.darkBackground : Colors.white;
    final placeholderColor = isDark ? AppColors.darkTextTertiary : AppColors.lightTextDisabled;

    return Dialog(
      backgroundColor: Colors.transparent,
      insetPadding: const EdgeInsets.symmetric(horizontal: 32, vertical: 24),
      child: ConstrainedBox(
        constraints: const BoxConstraints(maxWidth: 520),
        child: ClipRRect(
          borderRadius: BorderRadius.circular(18),
          child: Material(
            color: isDark ? AppColors.darkSurface : Colors.white,
            child: Column(
              mainAxisSize: MainAxisSize.min,
              children: [
                // ── Header ──
                Container(
                  padding: const EdgeInsets.symmetric(horizontal: 24, vertical: 18),
                  decoration: BoxDecoration(gradient: LinearGradient(colors: [_primary, _primaryDark])),
                  child: Row(
                    children: [
                      Expanded(
                        child: Text(
                          _isEditing ? 'Uredi Administratora' : 'Dodaj Administratora',
                          style: TextStyle(fontSize: 22, fontWeight: FontWeight.w700, color: onPrimaryColor),
                        ),
                      ),
                      InkWell(
                        borderRadius: BorderRadius.circular(8),
                        onTap: () => Navigator.of(context).pop(),
                        hoverColor: Colors.white12,
                        child: Padding(
                          padding: const EdgeInsets.all(6),
                          child: Icon(LucideIcons.x, color: onPrimaryColor, size: 20),
                        ),
                      ),
                    ],
                  ),
                ),

                // ── Form ──
                Flexible(
                  child: SingleChildScrollView(
                    padding: const EdgeInsets.all(24),
                    child: Form(
                      key: _formKey,
                      child: Column(
                        crossAxisAlignment: CrossAxisAlignment.start,
                        children: [
                          Row(
                            crossAxisAlignment: CrossAxisAlignment.start,
                            children: [
                              Expanded(
                                child: TextFormField(
                                  controller: _firstNameCtrl,
                                  decoration: _inputDecoration('Ime', prefixIcon: LucideIcons.user),
                                  style: TextStyle(fontSize: 14, color: textPrimary),
                                  validator: (v) {
                                    if (v == null || v.trim().isEmpty) return 'Ime je obavezno';
                                    if (v.trim().length < 2 || v.trim().length > 100) {
                                      return 'Ime mora biti između 2 i 100 karaktera';
                                    }
                                    return null;
                                  },
                                ),
                              ),
                              const SizedBox(width: 14),
                              Expanded(
                                child: TextFormField(
                                  controller: _lastNameCtrl,
                                  decoration: _inputDecoration('Prezime', prefixIcon: LucideIcons.user),
                                  style: TextStyle(fontSize: 14, color: textPrimary),
                                  validator: (v) {
                                    if (v == null || v.trim().isEmpty) return 'Prezime je obavezno';
                                    if (v.trim().length < 2 || v.trim().length > 100) {
                                      return 'Prezime mora biti između 2 i 100 karaktera';
                                    }
                                    return null;
                                  },
                                ),
                              ),
                            ],
                          ),
                          const SizedBox(height: 14),
                          TextFormField(
                            controller: _emailCtrl,
                            decoration: _inputDecoration('admin@example.com', prefixIcon: LucideIcons.mail),
                            style: TextStyle(fontSize: 14, color: textPrimary),
                            validator: (v) {
                              if (v == null || v.trim().isEmpty) return 'Email je obavezan';
                              if (v.trim().length > 255) return 'Email može imati maksimalno 255 karaktera';
                              final emailRegex = RegExp(r'^[a-zA-Z0-9._%+-]+@[a-zA-Z0-9.-]+\.[a-zA-Z]{2,}$');
                              if (!emailRegex.hasMatch(v.trim())) return 'Neispravan format email adrese';
                              return null;
                            },
                          ),
                          const SizedBox(height: 14),
                          TextFormField(
                            controller: _usernameCtrl,
                            decoration: _inputDecoration('korisnicko_ime', prefixIcon: LucideIcons.atSign),
                            style: TextStyle(fontSize: 14, color: textPrimary),
                            validator: (v) {
                              if (v == null || v.trim().isEmpty) return 'Korisničko ime je obavezno';
                              if (v.trim().length < 3 || v.trim().length > 50) {
                                return 'Korisničko ime mora biti između 3 i 50 karaktera';
                              }
                              final usernameRegex = RegExp(r'^[a-zA-Z0-9_.\-]+$');
                              if (!usernameRegex.hasMatch(v.trim())) {
                                return 'Samo slova, brojevi, tačka, crtica i podvlaka';
                              }
                              return null;
                            },
                          ),
                          if (!_isEditing) ...[
                            const SizedBox(height: 14),
                            TextFormField(
                              controller: _passwordCtrl,
                              obscureText: _obscurePassword,
                              decoration: _inputDecoration('Minimalno 8 karaktera', prefixIcon: LucideIcons.lock).copyWith(
                                suffixIcon: IconButton(
                                  icon: Icon(_obscurePassword ? LucideIcons.eyeOff : LucideIcons.eye,
                                      size: 18, color: placeholderColor),
                                  onPressed: () => setState(() => _obscurePassword = !_obscurePassword),
                                ),
                              ),
                              style: TextStyle(fontSize: 14, color: textPrimary),
                              validator: (v) {
                                if (v == null || v.isEmpty) return 'Lozinka je obavezna';
                                if (v.length < 8 || v.length > 100) {
                                  return 'Lozinka mora biti između 8 i 100 karaktera';
                                }
                                final passwordRegex = RegExp(
                                    r'^(?=.*[a-z])(?=.*[A-Z])(?=.*\d)(?=.*[@$!%*?&#])[A-Za-z\d@$!%*?&#]{8,}$');
                                if (!passwordRegex.hasMatch(v)) {
                                  return 'Mora sadržavati veliko, malo slovo, broj i specijalni karakter';
                                }
                                return null;
                              },
                            ),
                          ],
                          const SizedBox(height: 14),
                          TextFormField(
                            controller: _phoneCtrl,
                            decoration: _inputDecoration('+387 61 123 4567', prefixIcon: LucideIcons.phone),
                            style: TextStyle(fontSize: 14, color: textPrimary),
                            validator: Validators.phoneNumber,
                          ),
                        ],
                      ),
                    ),
                  ),
                ),

                // ── Footer ──
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
                          foregroundColor: isDark ? AppColors.darkTextSecondary : AppColors.lightTextSecondary,
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
                          boxShadow: [BoxShadow(color: _primary.withValues(alpha: 0.2), blurRadius: 8, offset: const Offset(0, 3))],
                        ),
                        child: Material(
                          color: Colors.transparent,
                          child: InkWell(
                            borderRadius: BorderRadius.circular(12),
                            onTap: _isSaving ? null : _submit,
                            child: Padding(
                              padding: const EdgeInsets.symmetric(horizontal: 24, vertical: 12),
                              child: _isSaving
                                  ? SizedBox(width: 18, height: 18, child: CircularProgressIndicator(strokeWidth: 2, color: onPrimaryColor))
                                  : Text(_isEditing ? 'Spremi Izmjene' : 'Dodaj Administratora',
                                      style: TextStyle(color: onPrimaryColor, fontWeight: FontWeight.w600, fontSize: 14)),
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
