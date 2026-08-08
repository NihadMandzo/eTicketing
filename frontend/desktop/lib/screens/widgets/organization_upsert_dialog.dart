import 'dart:io';

import 'package:file_picker/file_picker.dart';
import 'package:flutter/material.dart';
import 'package:lucide_icons_flutter/lucide_icons.dart';

import '../../main.dart';
import '../../models/requests/organization_insert_request.dart';
import '../../models/requests/organization_update_request.dart';
import '../../models/responses/organization_response.dart';
import '../../providers/organization_provider.dart';
import '../../theme/app_colors.dart';

class OrganizationUpsertDialog extends StatefulWidget {
  final OrganizationResponse? organization;
  final VoidCallback onSaved;

  const OrganizationUpsertDialog({
    super.key,
    this.organization,
    required this.onSaved,
  });

  @override
  State<OrganizationUpsertDialog> createState() =>
      _OrganizationUpsertDialogState();
}

class _OrganizationUpsertDialogState extends State<OrganizationUpsertDialog> {
  final _formKey = GlobalKey<FormState>();
  final _provider = OrganizationProvider();

  // Organization fields
  final _nameCtrl = TextEditingController();
  final _descCtrl = TextEditingController();
  final _addressCtrl = TextEditingController();
  final _phoneCtrl = TextEditingController();
  final _emailCtrl = TextEditingController();
  final _websiteCtrl = TextEditingController();

  // Admin fields (insert only)
  final _adminFirstNameCtrl = TextEditingController();
  final _adminLastNameCtrl = TextEditingController();
  final _adminEmailCtrl = TextEditingController();
  final _adminUsernameCtrl = TextEditingController();
  final _adminPasswordCtrl = TextEditingController();
  final _adminPhoneCtrl = TextEditingController();

  File? _logoFile;
  String? _logoFileName;
  bool _isSaving = false;
  bool _obscurePassword = true;

  bool get _isEditing => widget.organization != null;

  bool get _isDark => Theme.of(context).brightness == Brightness.dark;
  Color get _primary => _isDark ? AppColors.secondary : AppColors.primary;
  Color get _primaryDark => _isDark ? AppColors.primary : AppColors.primaryDark;

  @override
  void initState() {
    super.initState();
    if (_isEditing) {
      final org = widget.organization!;
      _nameCtrl.text = org.name;
      _descCtrl.text = org.description;
      _addressCtrl.text = org.address;
      _phoneCtrl.text = org.phoneNumber;
      _emailCtrl.text = org.email;
      _websiteCtrl.text = org.website ?? '';
    }
  }

  @override
  void dispose() {
    _nameCtrl.dispose();
    _descCtrl.dispose();
    _addressCtrl.dispose();
    _phoneCtrl.dispose();
    _emailCtrl.dispose();
    _websiteCtrl.dispose();
    _adminFirstNameCtrl.dispose();
    _adminLastNameCtrl.dispose();
    _adminEmailCtrl.dispose();
    _adminUsernameCtrl.dispose();
    _adminPasswordCtrl.dispose();
    _adminPhoneCtrl.dispose();
    super.dispose();
  }

  Future<void> _pickLogo() async {
    final result = await FilePicker.platform.pickFiles(
      type: FileType.custom,
      allowedExtensions: ['png', 'jpg', 'jpeg'],
      allowMultiple: false,
    );
    if (result != null && result.files.single.path != null) {
      setState(() {
        _logoFile = File(result.files.single.path!);
        _logoFileName = result.files.single.name;
      });
    }
  }

  Future<void> _submit() async {
    if (!_formKey.currentState!.validate()) return;

    setState(() => _isSaving = true);

    try {
      if (_isEditing) {
        await _provider.updateOrganization(
          widget.organization!.id,
          OrganizationUpdateRequest(
            name: _nameCtrl.text.trim(),
            description: _descCtrl.text.trim(),
            address: _addressCtrl.text.trim(),
            phoneNumber: _phoneCtrl.text.trim(),
            email: _emailCtrl.text.trim(),
            website: _websiteCtrl.text.trim().isEmpty
                ? null
                : _websiteCtrl.text.trim(),
            isActive: widget.organization!.isActive,
          ),
          logoFile: _logoFile,
        );
      } else {
        await _provider.insertOrganization(
          OrganizationInsertRequest(
            name: _nameCtrl.text.trim(),
            description: _descCtrl.text.trim(),
            address: _addressCtrl.text.trim(),
            phoneNumber: _phoneCtrl.text.trim(),
            email: _emailCtrl.text.trim(),
            website: _websiteCtrl.text.trim().isEmpty
                ? null
                : _websiteCtrl.text.trim(),
            adminFirstName: _adminFirstNameCtrl.text.trim(),
            adminLastName: _adminLastNameCtrl.text.trim(),
            adminEmail: _adminEmailCtrl.text.trim(),
            adminUsername: _adminUsernameCtrl.text.trim(),
            adminPassword: _adminPasswordCtrl.text,
            adminPhoneNumber: _adminPhoneCtrl.text.trim().isEmpty
                ? null
                : _adminPhoneCtrl.text.trim(),
          ),
          logoFile: _logoFile,
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

  // ── Shared decoration ──────────────────────────────────────────────

  InputDecoration _inputDecoration(String hint, {IconData? prefixIcon}) {
    final placeholderColor = _isDark ? AppColors.darkTextTertiary : AppColors.lightTextDisabled;
    final borderColor = _isDark ? AppColors.darkBorderInput : AppColors.lightBorderInput;
    return InputDecoration(
      hintText: hint,
      hintStyle: TextStyle(color: placeholderColor, fontSize: 13),
      contentPadding: const EdgeInsets.symmetric(horizontal: 14, vertical: 12),
      prefixIcon: prefixIcon != null
          ? Icon(prefixIcon, color: placeholderColor, size: 18)
          : null,
      border: OutlineInputBorder(
        borderRadius: BorderRadius.circular(12),
        borderSide: BorderSide(color: borderColor),
      ),
      enabledBorder: OutlineInputBorder(
        borderRadius: BorderRadius.circular(12),
        borderSide: BorderSide(color: borderColor),
      ),
      focusedBorder: OutlineInputBorder(
        borderRadius: BorderRadius.circular(12),
        borderSide: BorderSide(color: _primary, width: 2),
      ),
      errorBorder: OutlineInputBorder(
        borderRadius: BorderRadius.circular(12),
        borderSide: const BorderSide(color: AppColors.error),
      ),
      focusedErrorBorder: OutlineInputBorder(
        borderRadius: BorderRadius.circular(12),
        borderSide: const BorderSide(color: AppColors.error, width: 2),
      ),
      filled: true,
      fillColor: _isDark ? AppColors.darkInputFill : AppColors.lightInputFill,
    );
  }

  // ── Build ──────────────────────────────────────────────────────────

  @override
  Widget build(BuildContext context) {
    final isDark = _isDark;
    final textPrimary = isDark ? AppColors.darkTextPrimary : AppColors.lightTextPrimary;
    return Dialog(
      backgroundColor: Colors.transparent,
      insetPadding: const EdgeInsets.symmetric(horizontal: 32, vertical: 24),
      child: ConstrainedBox(
        constraints: const BoxConstraints(maxWidth: 720),
        child: ClipRRect(
          borderRadius: BorderRadius.circular(18),
          child: Material(
            color: isDark ? AppColors.darkSurface : Colors.white,
            child: Column(
              mainAxisSize: MainAxisSize.min,
              children: [
                // ── Header ──────────────────────────────────────────
                _buildHeader(),

                // ── Scrollable form ─────────────────────────────────
                Flexible(
                  child: SingleChildScrollView(
                    padding: const EdgeInsets.all(24),
                    child: Form(
                      key: _formKey,
                      child: Column(
                        crossAxisAlignment: CrossAxisAlignment.start,
                        children: [
                          _buildOrgDetailsSection(textPrimary),
                          const SizedBox(height: 28),
                          _buildContactSection(textPrimary),
                          const SizedBox(height: 28),
                          _buildLogoSection(),
                          if (!_isEditing) ...[
                            const SizedBox(height: 28),
                            _buildAdminSection(textPrimary),
                          ],
                        ],
                      ),
                    ),
                  ),
                ),

                // ── Footer ──────────────────────────────────────────
                _buildFooter(),
              ],
            ),
          ),
        ),
      ),
    );
  }

  // ── Header ──────────────────────────────────────────────────────────

  Widget _buildHeader() {
    final onPrimaryColor = _isDark ? AppColors.darkBackground : Colors.white;
    return Container(
      padding: const EdgeInsets.symmetric(horizontal: 24, vertical: 18),
      decoration: BoxDecoration(
        gradient: LinearGradient(
          colors: [_primary, _primaryDark],
        ),
      ),
      child: Row(
        children: [
          Expanded(
            child: Text(
              _isEditing ? 'Uredi Organizaciju' : 'Dodaj Novu Organizaciju',
              style: TextStyle(
                fontSize: 22,
                fontWeight: FontWeight.w700,
                color: onPrimaryColor,
              ),
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
    );
  }

  // ── Organization Details ────────────────────────────────────────────

  Widget _buildOrgDetailsSection(Color textPrimary) {
    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        _SectionHeader(
          icon: LucideIcons.building2,
          label: 'Detalji Organizacije',
        ),
        const SizedBox(height: 14),

        // Name (full width)
        _FieldLabel('Naziv Organizacije *'),
        const SizedBox(height: 6),
        TextFormField(
          controller: _nameCtrl,
          decoration: _inputDecoration('npr. Event Management Pro'),
          style: TextStyle(fontSize: 14, color: textPrimary),
          validator: (v) {
            if (v == null || v.trim().isEmpty) {
              return 'Naziv organizacije je obavezan';
            }
            if (v.trim().length < 2) {
              return 'Naziv mora biti između 2 i 200 karaktera';
            }
            if (v.trim().length > 200) {
              return 'Naziv mora biti između 2 i 200 karaktera';
            }
            return null;
          },
        ),

        const SizedBox(height: 14),

        // Website + Description
        Row(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Expanded(
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  _FieldLabel('Web Stranica'),
                  const SizedBox(height: 6),
                  TextFormField(
                    controller: _websiteCtrl,
                    decoration: _inputDecoration('https://example.com',
                        prefixIcon: LucideIcons.globe),
                    style: TextStyle(fontSize: 14, color: textPrimary),
                    validator: (v) {
                      if (v != null && v.trim().isNotEmpty) {
                        if (v.trim().length > 255) {
                          return 'Web stranica može imati maksimalno 255 karaktera';
                        }
                        final uri = Uri.tryParse(v.trim());
                        if (uri == null || !uri.hasScheme || !uri.hasAuthority) {
                          return 'Neispravan format web stranice (mora početi sa http/https)';
                        }
                      }
                      return null;
                    },
                  ),
                ],
              ),
            ),
          ],
        ),

        const SizedBox(height: 14),

        // Description (full width)
        _FieldLabel('Opis *'),
        const SizedBox(height: 6),
        TextFormField(
          controller: _descCtrl,
          maxLines: 3,
          decoration: _inputDecoration('Kratak opis organizacije...'),
          style: TextStyle(fontSize: 14, color: textPrimary),
          validator: (v) {
            if (v == null || v.trim().isEmpty) {
              return 'Opis je obavezan';
            }
            if (v.trim().length < 10) {
              return 'Opis mora biti između 10 i 1000 karaktera';
            }
            if (v.trim().length > 1000) {
              return 'Opis mora biti između 10 i 1000 karaktera';
            }
            return null;
          },
        ),
      ],
    );
  }

  // ── Contact Information ─────────────────────────────────────────────

  Widget _buildContactSection(Color textPrimary) {
    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        _SectionHeader(
          icon: LucideIcons.mail,
          label: 'Kontakt Informacije',
        ),
        const SizedBox(height: 14),

        // Email + Phone
        Row(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Expanded(
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  _FieldLabel('Email Adresa *'),
                  const SizedBox(height: 6),
                  TextFormField(
                    controller: _emailCtrl,
                    decoration: _inputDecoration('info@example.com',
                        prefixIcon: LucideIcons.mail),
                    style: TextStyle(fontSize: 14, color: textPrimary),
                    validator: (v) {
                      if (v == null || v.trim().isEmpty) {
                        return 'Email je obavezan';
                      }
                      if (v.trim().length > 255) {
                        return 'Email može imati maksimalno 255 karaktera';
                      }
                      final emailRegex = RegExp(
                          r'^[a-zA-Z0-9._%+-]+@[a-zA-Z0-9.-]+\.[a-zA-Z]{2,}$');
                      if (!emailRegex.hasMatch(v.trim())) {
                        return 'Neispravan format email adrese';
                      }
                      return null;
                    },
                  ),
                ],
              ),
            ),
            const SizedBox(width: 14),
            Expanded(
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  _FieldLabel('Broj Telefona *'),
                  const SizedBox(height: 6),
                  TextFormField(
                    controller: _phoneCtrl,
                    decoration: _inputDecoration('+387 61 123 4567',
                        prefixIcon: LucideIcons.phone),
                    style: TextStyle(fontSize: 14, color: textPrimary),
                    validator: (v) {
                      if (v == null || v.trim().isEmpty) {
                        return 'Broj telefona je obavezan';
                      }
                      if (v.trim().length > 20) {
                        return 'Broj telefona može imati maksimalno 20 karaktera';
                      }
                      return null;
                    },
                  ),
                ],
              ),
            ),
          ],
        ),

        const SizedBox(height: 14),

        // Address (full width)
        _FieldLabel('Adresa *'),
        const SizedBox(height: 6),
        TextFormField(
          controller: _addressCtrl,
          decoration: _inputDecoration(
              'Ulica Bulevar Meše Selimovića 12, Sarajevo',
              prefixIcon: LucideIcons.mapPin),
          style: TextStyle(fontSize: 14, color: textPrimary),
          validator: (v) {
            if (v == null || v.trim().isEmpty) {
              return 'Adresa je obavezna';
            }
            if (v.trim().length > 500) {
              return 'Adresa može imati maksimalno 500 karaktera';
            }
            return null;
          },
        ),
      ],
    );
  }

  // ── Logo ────────────────────────────────────────────────────────────

  Widget _buildLogoSection() {
    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        _SectionHeader(
          icon: LucideIcons.image,
          label: _isEditing
              ? 'Logo (ostavite prazno da zadržite postojeći)'
              : 'Logo Organizacije',
        ),
        const SizedBox(height: 10),
        _LogoPickerTile(
          logoFile: _logoFile,
          fileName: _logoFileName,
          onTap: _pickLogo,
        ),
      ],
    );
  }

  // ── Admin Details ───────────────────────────────────────────────────

  Widget _buildAdminSection(Color textPrimary) {
    final placeholderColor = _isDark ? AppColors.darkTextTertiary : AppColors.lightTextDisabled;
    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        _SectionHeader(
          icon: LucideIcons.userPlus,
          label: 'Administrator Organizacije',
        ),
        const SizedBox(height: 14),

        // First name + Last name
        Row(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Expanded(
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  _FieldLabel('Ime *'),
                  const SizedBox(height: 6),
                  TextFormField(
                    controller: _adminFirstNameCtrl,
                    decoration: _inputDecoration('Ime',
                        prefixIcon: LucideIcons.user),
                    style: TextStyle(fontSize: 14, color: textPrimary),
                    validator: (v) {
                      if (v == null || v.trim().isEmpty) {
                        return 'Ime administratora je obavezno';
                      }
                      if (v.trim().length < 2 || v.trim().length > 100) {
                        return 'Ime mora biti između 2 i 100 karaktera';
                      }
                      return null;
                    },
                  ),
                ],
              ),
            ),
            const SizedBox(width: 14),
            Expanded(
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  _FieldLabel('Prezime *'),
                  const SizedBox(height: 6),
                  TextFormField(
                    controller: _adminLastNameCtrl,
                    decoration: _inputDecoration('Prezime',
                        prefixIcon: LucideIcons.user),
                    style: TextStyle(fontSize: 14, color: textPrimary),
                    validator: (v) {
                      if (v == null || v.trim().isEmpty) {
                        return 'Prezime administratora je obavezno';
                      }
                      if (v.trim().length < 2 || v.trim().length > 100) {
                        return 'Prezime mora biti između 2 i 100 karaktera';
                      }
                      return null;
                    },
                  ),
                ],
              ),
            ),
          ],
        ),

        const SizedBox(height: 14),

        // Admin Email + Admin Username
        Row(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Expanded(
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  _FieldLabel('Email Administratora *'),
                  const SizedBox(height: 6),
                  TextFormField(
                    controller: _adminEmailCtrl,
                    decoration: _inputDecoration('admin@example.com',
                        prefixIcon: LucideIcons.mail),
                    style: TextStyle(fontSize: 14, color: textPrimary),
                    validator: (v) {
                      if (v == null || v.trim().isEmpty) {
                        return 'Email administratora je obavezan';
                      }
                      if (v.trim().length > 255) {
                        return 'Email može imati maksimalno 255 karaktera';
                      }
                      final emailRegex = RegExp(
                          r'^[a-zA-Z0-9._%+-]+@[a-zA-Z0-9.-]+\.[a-zA-Z]{2,}$');
                      if (!emailRegex.hasMatch(v.trim())) {
                        return 'Neispravan format email adrese';
                      }
                      return null;
                    },
                  ),
                ],
              ),
            ),
            const SizedBox(width: 14),
            Expanded(
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  _FieldLabel('Korisničko Ime *'),
                  const SizedBox(height: 6),
                  TextFormField(
                    controller: _adminUsernameCtrl,
                    decoration: _inputDecoration('korisnicko_ime',
                        prefixIcon: LucideIcons.atSign),
                    style: TextStyle(fontSize: 14, color: textPrimary),
                    validator: (v) {
                      if (v == null || v.trim().isEmpty) {
                        return 'Korisničko ime je obavezno';
                      }
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
                ],
              ),
            ),
          ],
        ),

        const SizedBox(height: 14),

        // Password + Admin Phone
        Row(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Expanded(
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  _FieldLabel('Lozinka *'),
                  const SizedBox(height: 6),
                  TextFormField(
                    controller: _adminPasswordCtrl,
                    obscureText: _obscurePassword,
                    decoration: _inputDecoration('Minimalno 8 karaktera',
                            prefixIcon: LucideIcons.lock)
                        .copyWith(
                      suffixIcon: IconButton(
                        icon: Icon(
                          _obscurePassword
                              ? LucideIcons.eyeOff
                              : LucideIcons.eye,
                          size: 18,
                          color: placeholderColor,
                        ),
                        onPressed: () => setState(
                            () => _obscurePassword = !_obscurePassword),
                      ),
                    ),
                    style: TextStyle(fontSize: 14, color: textPrimary),
                    validator: (v) {
                      if (v == null || v.isEmpty) {
                        return 'Lozinka administratora je obavezna';
                      }
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
              ),
            ),
            const SizedBox(width: 14),
            Expanded(
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  _FieldLabel('Broj Telefona Administratora'),
                  const SizedBox(height: 6),
                  TextFormField(
                    controller: _adminPhoneCtrl,
                    decoration: _inputDecoration('+387 61 123 4567',
                        prefixIcon: LucideIcons.phone),
                    style: TextStyle(fontSize: 14, color: textPrimary),
                    validator: (v) {
                      if (v != null && v.trim().isNotEmpty) {
                        if (v.trim().length > 20) {
                          return 'Broj telefona može imati maksimalno 20 karaktera';
                        }
                      }
                      return null;
                    },
                  ),
                ],
              ),
            ),
          ],
        ),
      ],
    );
  }

  // ── Footer ──────────────────────────────────────────────────────────

  Widget _buildFooter() {
    final isDark = _isDark;
    final onPrimaryColor = isDark ? AppColors.darkBackground : Colors.white;
    return Container(
      padding: const EdgeInsets.symmetric(horizontal: 24, vertical: 16),
      decoration: BoxDecoration(
        color: isDark ? AppColors.darkSurfaceSubtle : AppColors.lightSurfaceSubtle,
        border: Border(
          top: BorderSide(color: isDark ? AppColors.darkBorder : AppColors.lightBorder),
        ),
      ),
      child: Row(
        mainAxisAlignment: MainAxisAlignment.end,
        children: [
          OutlinedButton(
            onPressed: _isSaving ? null : () => Navigator.of(context).pop(),
            style: OutlinedButton.styleFrom(
              foregroundColor: isDark ? AppColors.darkTextSecondary : AppColors.lightTextSecondary,
              side: BorderSide(color: isDark ? AppColors.darkBorderInput : AppColors.lightBorderInput),
              shape: RoundedRectangleBorder(
                  borderRadius: BorderRadius.circular(12)),
              padding:
                  const EdgeInsets.symmetric(horizontal: 24, vertical: 12),
            ),
            child: const Text('Otkaži',
                style: TextStyle(fontWeight: FontWeight.w500, fontSize: 14)),
          ),
          const SizedBox(width: 12),
          Container(
            decoration: BoxDecoration(
              gradient: LinearGradient(
                colors: [_primary, _primaryDark],
              ),
              borderRadius: BorderRadius.circular(12),
              boxShadow: [
                BoxShadow(
                  color: _primary.withValues(alpha: 0.2),
                  blurRadius: 8,
                  offset: const Offset(0, 3),
                ),
              ],
            ),
            child: Material(
              color: Colors.transparent,
              child: InkWell(
                borderRadius: BorderRadius.circular(12),
                onTap: _isSaving ? null : _submit,
                child: Padding(
                  padding:
                      const EdgeInsets.symmetric(horizontal: 24, vertical: 12),
                  child: _isSaving
                      ? SizedBox(
                          width: 18,
                          height: 18,
                          child: CircularProgressIndicator(
                              strokeWidth: 2, color: onPrimaryColor),
                        )
                      : Text(
                          _isEditing
                              ? 'Spremi Izmjene'
                              : 'Dodaj Organizaciju',
                          style: TextStyle(
                            color: onPrimaryColor,
                            fontWeight: FontWeight.w600,
                            fontSize: 14,
                          ),
                        ),
                ),
              ),
            ),
          ),
        ],
      ),
    );
  }
}

// ─── Helper widgets ──────────────────────────────────────────────────────────

class _SectionHeader extends StatelessWidget {
  final IconData icon;
  final String label;

  const _SectionHeader({required this.icon, required this.label});

  @override
  Widget build(BuildContext context) {
    final isDark = Theme.of(context).brightness == Brightness.dark;
    final primaryColor = isDark ? AppColors.secondary : AppColors.primary;
    final textPrimary = isDark ? AppColors.darkTextPrimary : AppColors.lightTextPrimary;
    return Row(
      children: [
        Icon(icon, size: 20, color: primaryColor),
        const SizedBox(width: 8),
        Text(
          label,
          style: TextStyle(
            fontSize: 17,
            fontWeight: FontWeight.w700,
            color: textPrimary,
          ),
        ),
      ],
    );
  }
}

class _FieldLabel extends StatelessWidget {
  final String text;
  const _FieldLabel(this.text);

  @override
  Widget build(BuildContext context) {
    final isDark = Theme.of(context).brightness == Brightness.dark;
    return Text(
      text,
      style: TextStyle(
        fontSize: 13,
        fontWeight: FontWeight.w600,
        color: isDark ? AppColors.darkTextSecondary : AppColors.lightTextSecondary,
      ),
    );
  }
}

class _LogoPickerTile extends StatelessWidget {
  final File? logoFile;
  final String? fileName;
  final VoidCallback onTap;

  const _LogoPickerTile({
    required this.logoFile,
    required this.fileName,
    required this.onTap,
  });

  @override
  Widget build(BuildContext context) {
    final isDark = Theme.of(context).brightness == Brightness.dark;
    final primaryColor = isDark ? AppColors.secondary : AppColors.primary;
    final placeholderColor = isDark ? AppColors.darkTextTertiary : AppColors.lightTextDisabled;
    final borderColor = isDark ? AppColors.darkBorderInput : AppColors.lightBorderInput;
    final mutedBg = isDark ? AppColors.darkBorder : AppColors.lightBorder;
    final textPrimary = isDark ? AppColors.darkTextPrimary : AppColors.lightTextPrimary;
    final textTertiary = isDark ? AppColors.darkTextTertiary : AppColors.lightTextTertiary;

    final picked = logoFile != null;
    return GestureDetector(
      onTap: onTap,
      child: Container(
        padding: const EdgeInsets.symmetric(horizontal: 14, vertical: 10),
        decoration: BoxDecoration(
          color: isDark ? AppColors.darkSurfaceSubtle : AppColors.lightSurfaceSubtle,
          border: Border.all(
            color: picked ? primaryColor : borderColor,
            width: picked ? 2 : 1,
          ),
          borderRadius: BorderRadius.circular(12),
        ),
        child: Row(
          children: [
            Container(
              width: 44,
              height: 44,
              decoration: BoxDecoration(
                color: picked
                    ? primaryColor.withValues(alpha: isDark ? 0.18 : 0.1)
                    : mutedBg,
                borderRadius: BorderRadius.circular(10),
              ),
              child: picked
                  ? ClipRRect(
                      borderRadius: BorderRadius.circular(10),
                      child: Image.file(logoFile!, fit: BoxFit.cover),
                    )
                  : Icon(LucideIcons.upload, color: placeholderColor, size: 20),
            ),
            const SizedBox(width: 12),
            Expanded(
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  Text(
                    picked
                        ? (fileName ?? 'Odabran logo')
                        : 'Kliknite za odabir loga',
                    style: TextStyle(
                      fontSize: 13,
                      fontWeight: FontWeight.w500,
                      color: picked ? textPrimary : textTertiary,
                    ),
                    overflow: TextOverflow.ellipsis,
                  ),
                  Text(
                    'PNG, JPG, JPEG',
                    style: TextStyle(fontSize: 11, color: placeholderColor),
                  ),
                ],
              ),
            ),
            Icon(LucideIcons.folderOpen,
                size: 16, color: picked ? primaryColor : placeholderColor),
          ],
        ),
      ),
    );
  }
}
