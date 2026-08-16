import 'package:flutter/material.dart';
import 'package:lucide_icons_flutter/lucide_icons.dart';

import '../../models/requests/delete_admin_request.dart';
import '../../theme/app_colors.dart';

/// Confirmation dialog for deleting an OrganizationAdmin (SuperAdmin only —
/// see AdminService.DeleteAsync on the backend, which requires both fields
/// whenever the target is an OrganizationAdmin). Returns the entered
/// [DeleteAdminRequest] via Navigator.pop on submit, or null on cancel.
class DeleteOrganizationAdminDialog extends StatefulWidget {
  final String userFullName;

  const DeleteOrganizationAdminDialog({super.key, required this.userFullName});

  @override
  State<DeleteOrganizationAdminDialog> createState() => _DeleteOrganizationAdminDialogState();
}

class _DeleteOrganizationAdminDialogState extends State<DeleteOrganizationAdminDialog> {
  final _formKey = GlobalKey<FormState>();
  final _reasonCtrl = TextEditingController();
  final _recipientEmailCtrl = TextEditingController();

  bool get _isDark => Theme.of(context).brightness == Brightness.dark;

  @override
  void dispose() {
    _reasonCtrl.dispose();
    _recipientEmailCtrl.dispose();
    super.dispose();
  }

  InputDecoration _inputDecoration(String hint, {IconData? prefixIcon}) {
    final placeholderColor = _isDark ? AppColors.darkTextTertiary : AppColors.lightTextDisabled;
    final borderColor = _isDark ? AppColors.darkBorderInput : AppColors.lightBorderInput;
    final primary = _isDark ? AppColors.secondary : AppColors.primary;
    return InputDecoration(
      hintText: hint,
      hintStyle: TextStyle(color: placeholderColor, fontSize: 13),
      contentPadding: const EdgeInsets.symmetric(horizontal: 14, vertical: 12),
      prefixIcon: prefixIcon != null ? Icon(prefixIcon, color: placeholderColor, size: 18) : null,
      border: OutlineInputBorder(borderRadius: BorderRadius.circular(12), borderSide: BorderSide(color: borderColor)),
      enabledBorder: OutlineInputBorder(borderRadius: BorderRadius.circular(12), borderSide: BorderSide(color: borderColor)),
      focusedBorder: OutlineInputBorder(borderRadius: BorderRadius.circular(12), borderSide: BorderSide(color: primary, width: 2)),
      errorBorder: OutlineInputBorder(borderRadius: BorderRadius.circular(12), borderSide: const BorderSide(color: AppColors.error)),
      focusedErrorBorder: OutlineInputBorder(borderRadius: BorderRadius.circular(12), borderSide: const BorderSide(color: AppColors.error, width: 2)),
      filled: true,
      fillColor: _isDark ? AppColors.darkInputFill : AppColors.lightInputFill,
    );
  }

  void _submit() {
    if (!_formKey.currentState!.validate()) return;
    Navigator.of(context).pop(DeleteAdminRequest(
      reason: _reasonCtrl.text.trim(),
      recipientEmail: _recipientEmailCtrl.text.trim(),
    ));
  }

  @override
  Widget build(BuildContext context) {
    final isDark = _isDark;
    final textPrimary = isDark ? AppColors.darkTextPrimary : AppColors.lightTextPrimary;
    final textSecondary = isDark ? AppColors.darkTextSecondary : AppColors.lightTextSecondary;

    return Dialog(
      backgroundColor: Colors.transparent,
      insetPadding: const EdgeInsets.symmetric(horizontal: 32, vertical: 24),
      child: ConstrainedBox(
        constraints: const BoxConstraints(maxWidth: 480),
        child: ClipRRect(
          borderRadius: BorderRadius.circular(18),
          child: Material(
            color: isDark ? AppColors.darkSurface : Colors.white,
            child: Column(
              mainAxisSize: MainAxisSize.min,
              children: [
                Container(
                  padding: const EdgeInsets.symmetric(horizontal: 24, vertical: 18),
                  decoration: BoxDecoration(gradient: LinearGradient(colors: [AppColors.errorDark, AppColors.error])),
                  child: Row(
                    children: [
                      Expanded(
                        child: Text(
                          'Obriši Administratora Organizacije',
                          style: TextStyle(fontSize: 18, fontWeight: FontWeight.w700, color: Colors.white),
                        ),
                      ),
                      InkWell(
                        borderRadius: BorderRadius.circular(8),
                        onTap: () => Navigator.of(context).pop(),
                        child: const Padding(
                          padding: EdgeInsets.all(6),
                          child: Icon(LucideIcons.x, color: Colors.white, size: 20),
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
                            'Brišete administratora "${widget.userFullName}". Ova akcija se ne može poništiti.',
                            style: TextStyle(fontSize: 14, color: textSecondary),
                          ),
                          const SizedBox(height: 20),
                          Text('Razlog Brisanja *',
                              style: TextStyle(fontSize: 13, fontWeight: FontWeight.w600, color: textSecondary)),
                          const SizedBox(height: 6),
                          TextFormField(
                            controller: _reasonCtrl,
                            maxLines: 3,
                            maxLength: 500,
                            decoration: _inputDecoration('Objasnite zašto se ovaj administrator uklanja...'),
                            style: TextStyle(fontSize: 14, color: textPrimary),
                            validator: (v) {
                              if (v == null || v.trim().isEmpty) return 'Razlog brisanja je obavezan';
                              if (v.trim().length > 500) return 'Razlog može imati maksimalno 500 karaktera';
                              return null;
                            },
                          ),
                          const SizedBox(height: 14),
                          Text('Email Organizacije za Obavještenje *',
                              style: TextStyle(fontSize: 13, fontWeight: FontWeight.w600, color: textSecondary)),
                          const SizedBox(height: 6),
                          TextFormField(
                            controller: _recipientEmailCtrl,
                            decoration: _inputDecoration('kontakt@organizacija.ba', prefixIcon: LucideIcons.mail),
                            style: TextStyle(fontSize: 14, color: textPrimary),
                            validator: (v) {
                              if (v == null || v.trim().isEmpty) return 'Email za obavještenje je obavezan';
                              final emailRegex = RegExp(r'^[a-zA-Z0-9._%+-]+@[a-zA-Z0-9.-]+\.[a-zA-Z]{2,}$');
                              if (!emailRegex.hasMatch(v.trim())) return 'Neispravan format email adrese';
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
                        onPressed: () => Navigator.of(context).pop(),
                        style: OutlinedButton.styleFrom(
                          foregroundColor: textSecondary,
                          side: BorderSide(color: isDark ? AppColors.darkBorderInput : AppColors.lightBorderInput),
                          shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(12)),
                          padding: const EdgeInsets.symmetric(horizontal: 24, vertical: 12),
                        ),
                        child: const Text('Odustani', style: TextStyle(fontWeight: FontWeight.w500, fontSize: 14)),
                      ),
                      const SizedBox(width: 12),
                      ElevatedButton(
                        onPressed: _submit,
                        style: ElevatedButton.styleFrom(
                          backgroundColor: AppColors.errorDark,
                          foregroundColor: Colors.white,
                          shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(12)),
                          padding: const EdgeInsets.symmetric(horizontal: 24, vertical: 12),
                        ),
                        child: const Text('Obriši', style: TextStyle(fontWeight: FontWeight.w600, fontSize: 14)),
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
