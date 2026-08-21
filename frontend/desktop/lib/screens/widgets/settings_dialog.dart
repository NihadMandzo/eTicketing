import 'dart:io';
import 'dart:typed_data';

import 'package:file_picker/file_picker.dart';
import 'package:flutter/material.dart';

import '../../models/requests/change_password_request.dart';
import '../../models/requests/organization_update_request.dart';
import '../../models/requests/update_user_request.dart';
import '../../models/responses/organization_response.dart';
import '../../models/responses/user_profile.dart';
import '../../providers/api_exception.dart';
import '../../providers/auth_provider.dart';
import '../../providers/organization_provider.dart';
import '../../theme/app_colors.dart';
import '../../utility/image_validation.dart';
import 'image_crop_dialog.dart';

// ── Allowed roles that can see the Org tab ────────────────────────────────────
const _kOrgRoles = {'OrganizationSuperAdmin', 'OrganizationAdmin'};

const Color _kPrimary = AppColors.primary;
const Color _kPrimaryDark = AppColors.primaryDark;

// ─── Entry point: call this to open the dialog ────────────────────────────────

Future<UserProfile?> showSettingsDialog(BuildContext context, UserProfile user) {
  return showDialog<UserProfile>(
    context: context,
    barrierColor: Colors.black54,
    builder: (_) => SettingsDialog(user: user),
  );
}

// ─── Main dialog widget ───────────────────────────────────────────────────────

class SettingsDialog extends StatefulWidget {
  final UserProfile user;

  const SettingsDialog({super.key, required this.user});

  @override
  State<SettingsDialog> createState() => _SettingsDialogState();
}

class _SettingsDialogState extends State<SettingsDialog> {
  // ── Providers
  final _authProvider = AuthProvider();
  final _orgProvider = OrganizationProvider();

  // ── Mode: 'profile' | 'organization'
  String _mode = 'profile';

  // ── Active tabs
  String _profileTab = 'profile';
  String _orgTab = 'info';

  // ── Profile form
  late final TextEditingController _firstName;
  late final TextEditingController _lastName;
  late final TextEditingController _username;
  late final TextEditingController _email;
  late final TextEditingController _phone;

  // ── Security form
  final _currentPwd = TextEditingController();
  final _newPwd = TextEditingController();
  final _confirmPwd = TextEditingController();
  // ── Org form
  final _orgName = TextEditingController();
  final _orgDesc = TextEditingController();
  final _orgEmail = TextEditingController();
  final _orgPhone = TextEditingController();
  final _orgAddress = TextEditingController();
  final _orgWebsite = TextEditingController();
  final _roleCtrl = TextEditingController();
  final _orgNameProfileCtrl = TextEditingController();
  bool _orgActive = true;
  Uint8List? _logoBytes;

  bool _saving = false;
  bool _loadingOrg = false;
  String? _orgId;
  OrganizationResponse? _orgData;
  UserProfile? _updatedUser;

  bool get _hasOrg =>
      widget.user.organizationId != null &&
      _kOrgRoles.contains(widget.user.roleName);

  // ── Dark-mode-aware color helpers (mirrors the pattern in login_screen.dart) ──
  bool get _isDark => Theme.of(context).brightness == Brightness.dark;
  Color get _primary => _isDark ? AppColors.secondary : AppColors.primary;
  Color get _primaryDark => _isDark ? AppColors.primary : AppColors.primaryDark;
  Color get _onPrimary => _isDark ? AppColors.darkBackground : Colors.white;

  // ── Profile tabs
  static const _profileTabs = [
    _TabDef('profile', 'Moj Profil', Icons.person_rounded),
    _TabDef('security', 'Sigurnost', Icons.lock_rounded),
  ];

  // ── Org tabs
  static const _orgTabs = [
    _TabDef('info', 'Informacije', Icons.business_rounded),
    _TabDef('contact', 'Kontakt', Icons.contact_mail_rounded),
    _TabDef('settings', 'Postavke', Icons.settings_rounded),
  ];

  @override
  void initState() {
    super.initState();
    final u = widget.user;
    _firstName = TextEditingController(text: u.firstName);
    _lastName = TextEditingController(text: u.lastName);
    _username = TextEditingController(text: u.username);
    _email = TextEditingController(text: u.email);
    _phone = TextEditingController(text: u.phoneNumber ?? '');
    _roleCtrl.text = _roleLabel(u.roleName);
    _orgNameProfileCtrl.text = u.organizationName ?? '';

    _orgId = u.organizationId;

    if (_hasOrg && _orgId != null) {
      _fetchOrganization();
    }
  }

  Future<void> _fetchOrganization() async {
    setState(() => _loadingOrg = true);
    try {
      final org = await _orgProvider.getOrganization(_orgId!);
      if (!mounted) return;
      _orgData = org;
      _orgName.text = org.name;
      _orgDesc.text = org.description;
      _orgEmail.text = org.email;
      _orgPhone.text = org.phoneNumber;
      _orgAddress.text = org.address;
      _orgWebsite.text = org.website ?? '';
      _orgActive = org.isActive;
    } on ApiException catch (e) {
      if (mounted) _showError(e.apiError.displayMessage);
    } catch (e) {
      if (mounted) _showError('Greška pri učitavanju organizacije.');
    } finally {
      if (mounted) setState(() => _loadingOrg = false);
    }
  }

  @override
  void dispose() {
    for (final c in [
      _firstName, _lastName, _username, _email, _phone,
      _currentPwd, _newPwd, _confirmPwd,
      _orgName, _orgDesc, _orgEmail, _orgPhone, _orgAddress, _orgWebsite,
      _roleCtrl, _orgNameProfileCtrl,
    ]) {
      c.dispose();
    }
    super.dispose();
  }

  // ── Save handler ──────────────────────────────────────────────────────────

  Future<void> _handleSave() async {
    setState(() => _saving = true);
    try {
      if (_mode == 'profile') {
        await _saveProfile();
      } else {
        await _saveOrganization();
      }
    } on ApiException catch (e) {
      if (mounted) _showError(e.apiError.displayMessage);
    } catch (e) {
      if (mounted) _showError('Došlo je do greške.');
    } finally {
      if (mounted) setState(() => _saving = false);
    }
  }

  Future<void> _saveProfile() async {
    if (_profileTab == 'security') {
      // ── Change password ──
      if (_newPwd.text.isEmpty && _currentPwd.text.isEmpty) {
        _showError('Unesite lozinku za promjenu.');
        return;
      }
      if (_newPwd.text != _confirmPwd.text) {
        _showError('Lozinke se ne podudaraju.');
        return;
      }
      if (_currentPwd.text.isEmpty) {
        _showError('Unesite trenutnu lozinku.');
        return;
      }
      await _authProvider.changePassword(ChangePasswordRequest(
        currentPassword: _currentPwd.text,
        newPassword: _newPwd.text,
        confirmPassword: _confirmPwd.text,
      ));
      if (mounted) {
        _currentPwd.clear();
        _newPwd.clear();
        _confirmPwd.clear();
        _showSuccess('Lozinka uspješno promijenjena.');
      }
    } else {
      // ── Update user profile ──
      final updated = await _authProvider.updateUser(UpdateUserRequest(
        firstName: _firstName.text.trim(),
        lastName: _lastName.text.trim(),
        username: _username.text.trim(),
        phoneNumber: _phone.text.trim().isEmpty ? null : _phone.text.trim(),
      ));
      if (mounted) {
        // Refresh form fields from the response
        _updatedUser = updated;
        _firstName.text = updated.firstName;
        _lastName.text = updated.lastName;
        _username.text = updated.username;
        _email.text = updated.email;
        _phone.text = updated.phoneNumber ?? '';
        _showSuccess('Profil uspješno ažuriran.');
      }
    }
  }

  Future<void> _saveOrganization() async {
    if (_orgId == null) {
      _showError('Organizacija nije pronađena.');
      return;
    }
    // Metadata is always saved first, as a plain JSON request — the logo (if
    // a new one was picked) is a separate dedicated call afterwards, never
    // bundled in.
    var updated = await _orgProvider.updateOrganization(
      _orgId!,
      OrganizationUpdateRequest(
        name: _orgName.text.trim(),
        description: _orgDesc.text.trim(),
        address: _orgAddress.text.trim(),
        phoneNumber: _orgPhone.text.trim(),
        email: _orgEmail.text.trim(),
        website:
            _orgWebsite.text.trim().isEmpty ? null : _orgWebsite.text.trim(),
        isActive: _orgActive,
      ),
    );

    if (_logoBytes != null) {
      // An existing logo can only be replaced via PUT; an organization that
      // doesn't have one yet needs the create (POST) call instead.
      final hasExistingLogo = _orgData?.logoUrl != null;
      updated = hasExistingLogo
          ? await _orgProvider.replaceLogo(_orgId!, _logoBytes!)
          : await _orgProvider.createLogo(_orgId!, _logoBytes!);
    }

    if (mounted) {
      // Refresh form fields from the response
      _orgData = updated;
      _orgName.text = updated.name;
      _orgDesc.text = updated.description;
      _orgEmail.text = updated.email;
      _orgPhone.text = updated.phoneNumber;
      _orgAddress.text = updated.address;
      _orgWebsite.text = updated.website ?? '';
      setState(() {
        _orgActive = updated.isActive;
        _logoBytes = null;
      });
      _showSuccess('Organizacija uspješno ažurirana.');
    }
  }

  void _showError(String msg) {
    ScaffoldMessenger.of(context).showSnackBar(SnackBar(
      content: Text(msg),
      backgroundColor: AppColors.errorDark,
    ));
  }

  void _showSuccess(String msg) {
    ScaffoldMessenger.of(context).showSnackBar(SnackBar(
      content: Text(msg),
      backgroundColor: AppColors.successDark,
    ));
  }

  // ── Initials helper ──────────────────────────────────────────────────────

  String _userInitials(UserProfile u) {
    final f = u.firstName.isNotEmpty ? u.firstName[0] : '';
    final l = u.lastName.isNotEmpty ? u.lastName[0] : '';
    return f.isEmpty && l.isEmpty ? '?' : '$f$l';
  }

  // ── Role display ─────────────────────────────────────────────────────────

  String _roleLabel(String r) {
    switch (r) {
      case 'SuperAdmin':
        return 'Super Administrator';
      case 'Admin':
        return 'Administrator';
      case 'OrganizationSuperAdmin':
        return 'Org. Super Administrator';
      case 'OrganizationAdmin':
        return 'Org. Administrator';
      default:
        return r;
    }
  }

  // ─────────────────────────────────────────────────────────────────────────

  void _closeDialog() {
    Navigator.of(context).pop(_updatedUser);
  }

  @override
  Widget build(BuildContext context) {
    return Dialog(
      backgroundColor: Colors.transparent,
      insetPadding: EdgeInsets.zero,
      child: ClipRRect(
        borderRadius: BorderRadius.circular(20),
        child: Container(
          width: 1100,
          height: 800,
          color: _isDark ? AppColors.darkSurface : Colors.white,
          child: Column(
            children: [
              _buildHeader(),
              _buildModeSelector(),
              Expanded(
                child: Row(
                  crossAxisAlignment: CrossAxisAlignment.stretch,
                  children: [
                    _buildSidebarTabs(),
                    Expanded(child: _buildContent()),
                  ],
                ),
              ),
              _buildFooter(),
            ],
          ),
        ),
      ),
    );
  }

  // ── Header ────────────────────────────────────────────────────────────────

  Widget _buildHeader() {
    final isProfile = _mode == 'profile';
    final onHeader = _onPrimary;
    return Container(
      padding: const EdgeInsets.fromLTRB(24, 20, 20, 20),
      decoration: BoxDecoration(
        gradient: LinearGradient(colors: [_primary, _primaryDark]),
      ),
      child: Row(
        children: [
          Container(
            width: 44,
            height: 44,
            decoration: BoxDecoration(
              color: onHeader.withValues(alpha: 0.2),
              borderRadius: BorderRadius.circular(12),
            ),
            child: Icon(
              isProfile ? Icons.person_rounded : Icons.business_rounded,
              color: onHeader,
              size: 22,
            ),
          ),
          const SizedBox(width: 14),
          Column(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              Text(
                isProfile ? 'Postavke Profila' : 'Postavke Organizacije',
                style: TextStyle(
                  fontSize: 22,
                  fontWeight: FontWeight.w700,
                  color: onHeader,
                ),
              ),
              Text(
                isProfile
                    ? 'Upravljajte svojim nalogom i postavkama'
                    : 'Upravljajte informacijama o organizaciji',
                style: TextStyle(
                  fontSize: 13,
                  color: onHeader.withValues(alpha: 0.8),
                ),
              ),
            ],
          ),
          const Spacer(),
          _IconBtn(
            icon: Icons.close_rounded,
            onTap: _closeDialog,
            color: onHeader.withValues(alpha: 0.8),
            hoverColor: onHeader,
            hoverBg: onHeader.withValues(alpha: 0.1),
          ),
        ],
      ),
    );
  }

  // ── Mode selector ─────────────────────────────────────────────────────────

  Widget _buildModeSelector() {
    return Container(
      padding: const EdgeInsets.symmetric(horizontal: 16, vertical: 10),
      decoration: BoxDecoration(
        color: _isDark ? AppColors.darkSurfaceSubtle : AppColors.lightSurfaceSubtle,
        border: Border(
          bottom: BorderSide(color: _isDark ? AppColors.darkBorder : AppColors.lightBorder),
        ),
      ),
      child: Row(
        children: [
          _ModeTabBtn(
            icon: Icons.person_rounded,
            label: 'Moj Profil',
            active: _mode == 'profile',
            onTap: () => setState(() => _mode = 'profile'),
          ),
          if (_hasOrg) ...[
            const SizedBox(width: 8),
            _ModeTabBtn(
              icon: Icons.business_rounded,
              label: 'Organizacija',
              active: _mode == 'organization',
              onTap: () => setState(() => _mode = 'organization'),
            ),
          ],
        ],
      ),
    );
  }

  // ── Sidebar tabs ──────────────────────────────────────────────────────────

  Widget _buildSidebarTabs() {
    final tabs = _mode == 'profile' ? _profileTabs : _orgTabs;
    final currentTab = _mode == 'profile' ? _profileTab : _orgTab;

    return Container(
      width: 220,
      decoration: BoxDecoration(
        color: _isDark ? AppColors.darkSurfaceSubtle : AppColors.lightSurfaceSubtle,
        border: Border(
          right: BorderSide(color: _isDark ? AppColors.darkBorder : AppColors.lightBorder),
        ),
      ),
      child: SingleChildScrollView(
        padding: const EdgeInsets.all(12),
        child: Column(
          children: [
            ...tabs.map((t) => _SideTabBtn(
                  tab: t,
                  active: currentTab == t.id,
                  onTap: () => setState(() {
                    if (_mode == 'profile') {
                      _profileTab = t.id;
                    } else {
                      _orgTab = t.id;
                    }
                  }),
                )),
            const SizedBox(height: 16),
            _buildInfoCard(),
          ],
        ),
      ),
    );
  }

  Widget _buildInfoCard() {
    final u = widget.user;
    final isProfile = _mode == 'profile';
    final isDark = _isDark;
    final borderColor = isDark ? AppColors.darkBorder : AppColors.lightBorder;
    final textPrimary = isDark ? AppColors.darkTextPrimary : AppColors.lightTextPrimary;
    final textTertiary = isDark ? AppColors.darkTextTertiary : AppColors.lightTextTertiary;

    return Container(
      padding: const EdgeInsets.all(14),
      decoration: BoxDecoration(
        color: isDark ? AppColors.darkSurface : Colors.white,
        borderRadius: BorderRadius.circular(12),
        border: Border.all(color: borderColor),
      ),
      child: isProfile
          ? Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Row(
                  children: [
                    _Avatar(
                        initials: _userInitials(u),
                        size: 44),
                    const SizedBox(width: 10),
                    Expanded(
                      child: Column(
                        crossAxisAlignment: CrossAxisAlignment.start,
                        children: [
                          Text(u.fullName,
                              maxLines: 1,
                              overflow: TextOverflow.ellipsis,
                              style: TextStyle(
                                fontSize: 13,
                                fontWeight: FontWeight.w600,
                                color: textPrimary,
                              )),
                          Text(u.email,
                              maxLines: 1,
                              overflow: TextOverflow.ellipsis,
                              style: TextStyle(
                                fontSize: 11,
                                color: textTertiary,
                              )),
                        ],
                      ),
                    ),
                  ],
                ),
                const SizedBox(height: 10),
                Divider(height: 1, color: borderColor),
                const SizedBox(height: 10),
                Container(
                  padding:
                      const EdgeInsets.symmetric(horizontal: 8, vertical: 4),
                  decoration: BoxDecoration(
                    color: _primary.withValues(alpha: isDark ? 0.18 : 0.1),
                    borderRadius: BorderRadius.circular(8),
                  ),
                  child: Text(
                    _roleLabel(u.roleName),
                    style: TextStyle(
                      fontSize: 11,
                      fontWeight: FontWeight.w600,
                      color: _primary,
                    ),
                  ),
                ),
              ],
            )
          : Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Row(
                  children: [
                    Container(
                      width: 44,
                      height: 44,
                      decoration: BoxDecoration(
                        gradient: LinearGradient(
                          colors: [_primary, _primaryDark],
                        ),
                        shape: BoxShape.circle,
                      ),
                      child: Icon(Icons.business_rounded,
                          color: _onPrimary, size: 24),
                    ),
                    const SizedBox(width: 10),
                    Expanded(
                      child: Column(
                        crossAxisAlignment: CrossAxisAlignment.start,
                        children: [
                          Text(
                              _orgData?.name ??
                                  u.organizationName ??
                                  '-',
                              maxLines: 1,
                              overflow: TextOverflow.ellipsis,
                              style: TextStyle(
                                fontSize: 13,
                                fontWeight: FontWeight.w600,
                                color: textPrimary,
                              )),
                          Text('Organizacija',
                              style: TextStyle(
                                  fontSize: 11, color: textTertiary)),
                        ],
                      ),
                    ),
                  ],
                ),
                const SizedBox(height: 10),
                Divider(height: 1, color: borderColor),
                const SizedBox(height: 10),
                Container(
                  padding:
                      const EdgeInsets.symmetric(horizontal: 8, vertical: 4),
                  decoration: BoxDecoration(
                    color: _orgActive
                        ? AppColors.success.withValues(alpha: isDark ? 0.18 : 0.1)
                        : AppColors.error.withValues(alpha: isDark ? 0.18 : 0.1),
                    borderRadius: BorderRadius.circular(8),
                  ),
                  child: Text(
                    _orgActive ? 'Aktivna' : 'Neaktivna',
                    style: TextStyle(
                      fontSize: 11,
                      fontWeight: FontWeight.w600,
                      color: _orgActive
                          ? AppColors.successDark
                          : AppColors.errorDark,
                    ),
                  ),
                ),
              ],
            ),
    );
  }

  // ── Main content ──────────────────────────────────────────────────────────

  Widget _buildContent() {
    if (_mode == 'organization' && _loadingOrg) {
      return Center(
        child: Padding(
          padding: const EdgeInsets.all(48),
          child: CircularProgressIndicator(color: _primary),
        ),
      );
    }
    return SingleChildScrollView(
      padding: const EdgeInsets.all(24),
      child:
          _mode == 'profile' ? _buildProfileContent() : _buildOrgContent(),
    );
  }

  // ── Profile content ───────────────────────────────────────────────────────

  Widget _buildProfileContent() {
    switch (_profileTab) {
      case 'security':
        return _buildSecurityTab();
      default:
        return _buildProfileTab();
    }
  }

  Widget _buildProfileTab() {
    final u = widget.user;
    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        _sectionTitle('Informacije o Profilu'),
        const SizedBox(height: 16),
        _AvatarCard(initials: _userInitials(u), name: u.fullName),
        const SizedBox(height: 20),
        _Grid(children: [
          _Field(label: 'Ime *', controller: _firstName, hint: 'Unesite ime'),
          _Field(
              label: 'Prezime *',
              controller: _lastName,
              hint: 'Unesite prezime'),
          _Field(
              label: 'Korisničko Ime *',
              controller: _username,
              hint: 'korisnik123'),
          _Field(
              label: 'Email Adresa *',
              controller: _email,
              hint: 'vi@primjer.ba',
              prefixIcon: Icons.mail_outline_rounded,
              readOnly: true),
          _Field(
              label: 'Broj Telefona',
              controller: _phone,
              hint: '+387 61 123 456',
              prefixIcon: Icons.phone_outlined,
              keyboardType: TextInputType.phone),
          _Field(
              label: 'Uloga',
              controller: _roleCtrl,
              readOnly: true),
          if (u.organizationName != null)
            _Field(
              label: 'Organizacija',
              controller: _orgNameProfileCtrl,
              readOnly: true,
              fullWidth: true,
            ),
        ]),
      ],
    );
  }

  Widget _buildSecurityTab() {
    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        _sectionTitle('Sigurnost Naloga'),
        const SizedBox(height: 16),
        _InfoBanner(
          icon: Icons.shield_outlined,
          color: AppColors.warning,
          bg: _isDark ? AppColors.warning.withValues(alpha: 0.15) : AppColors.warningBg,
          border: _isDark ? AppColors.warning.withValues(alpha: 0.4) : AppColors.warningBorder,
          title: 'Promjena Lozinke',
          body:
              'Preporučujemo jaku lozinku sa najmanje 8 karaktera, brojevima i simbolima.',
        ),
        const SizedBox(height: 16),
        _Field(
          label: 'Trenutna Lozinka',
          controller: _currentPwd,
          hint: 'Unesite trenutnu lozinku',
          prefixIcon: Icons.lock_outline_rounded,
          obscure: true,
        ),
        const SizedBox(height: 14),
        _Field(
          label: 'Nova Lozinka',
          controller: _newPwd,
          hint: 'Unesite novu lozinku',
          prefixIcon: Icons.lock_outline_rounded,
          obscure: true,
        ),
        const SizedBox(height: 14),
        _Field(
          label: 'Potvrdite Novu Lozinku',
          controller: _confirmPwd,
          hint: 'Potvrdite novu lozinku',
          prefixIcon: Icons.lock_outline_rounded,
          obscure: true,
        ),
      ],
    );
  }


  // ── Org content ───────────────────────────────────────────────────────────

  Widget _buildOrgContent() {
    switch (_orgTab) {
      case 'contact':
        return _buildOrgContactTab();
      case 'settings':
        return _buildOrgSettingsTab();
      default:
        return _buildOrgInfoTab();
    }
  }

  Widget _buildOrgInfoTab() {
    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        _sectionTitle('Informacije o Organizaciji'),
        const SizedBox(height: 16),
        _OrgLogoCard(
          logoUrl: _orgData?.logoUrl,
          logoBytes: _logoBytes,
          onPickLogo: () async {
            final result = await FilePicker.platform.pickFiles(
              type: FileType.custom,
              allowedExtensions: ['png', 'jpg', 'jpeg'],
            );
            if (result == null || result.files.single.path == null) return;

            final path = result.files.single.path!;
            final bytes = await File(path).readAsBytes();
            if (!mounted) return;
            final cropped = await ImageCropDialog.show(context, bytes);
            if (cropped == null) return;

            // Mirrors OrganizationLogoValidation on the backend — that
            // validator is still authoritative and re-checks regardless
            // (see 00-workflow-and-testing.md), this just gives instant
            // feedback instead of a round-trip to the API.
            final error = ImageValidation.validateMaxBytes(
              cropped,
              maxBytes: 1 * 1024 * 1024,
              sizeErrorMessage: 'Logo može biti maksimalno 1MB.',
            );
            if (error != null) {
              if (mounted) _showError(error);
              return;
            }

            if (mounted) setState(() => _logoBytes = cropped);
          },
        ),
        const SizedBox(height: 20),
        _Field(
            label: 'Naziv Organizacije *',
            controller: _orgName,
            hint: 'npr. Event Management Pro'),
        const SizedBox(height: 14),
        _Field(
          label: 'Opis',
          controller: _orgDesc,
          hint: 'Unesite kratak opis organizacije...',
          maxLines: 4,
        ),
      ],
    );
  }

  Widget _buildOrgContactTab() {
    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        _sectionTitle('Kontakt Informacije'),
        const SizedBox(height: 16),
        _Grid(children: [
          _Field(
              label: 'Email Adresa *',
              controller: _orgEmail,
              hint: 'info@organizacija.ba',
              prefixIcon: Icons.mail_outline_rounded,
              keyboardType: TextInputType.emailAddress),
          _Field(
              label: 'Broj Telefona *',
              controller: _orgPhone,
              hint: '+387 33 123 456',
              prefixIcon: Icons.phone_outlined,
              keyboardType: TextInputType.phone),
          _Field(
              label: 'Adresa *',
              controller: _orgAddress,
              hint: 'Ulica i broj, Grad',
              prefixIcon: Icons.location_on_outlined,
              maxLines: 2,
              fullWidth: true),
          _Field(
              label: 'Web Stranica',
              controller: _orgWebsite,
              hint: 'https://www.organizacija.ba',
              prefixIcon: Icons.link_rounded,
              fullWidth: true),
        ]),
      ],
    );
  }

  Widget _buildOrgSettingsTab() {
    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        _sectionTitle('Postavke Organizacije'),
        const SizedBox(height: 16),
        _ToggleRow(
          title: 'Status Organizacije',
          subtitle:
              'Aktivna organizacija može kreirati i upravljati događajima',
          value: _orgActive,
          onChanged: (v) => setState(() => _orgActive = v),
        ),
        const SizedBox(height: 12),
        _InfoBanner(
          icon: Icons.info_outline_rounded,
          color: AppColors.info,
          bg: _isDark ? AppColors.info.withValues(alpha: 0.15) : AppColors.infoBg,
          border: _isDark ? AppColors.info.withValues(alpha: 0.4) : AppColors.infoBorder,
          title: 'Napomena',
          body:
              'Promjene u postavkama organizacije će biti vidljive svim korisnicima koji '
              'pripadaju ovoj organizaciji.',
        ),
      ],
    );
  }

  // ── Footer ────────────────────────────────────────────────────────────────

  Widget _buildFooter() {
    return Container(
      padding: const EdgeInsets.fromLTRB(24, 14, 24, 14),
      decoration: BoxDecoration(
        color: _isDark ? AppColors.darkSurfaceSubtle : AppColors.lightSurfaceSubtle,
        border: Border(
          top: BorderSide(color: _isDark ? AppColors.darkBorder : AppColors.lightBorder),
        ),
      ),
      child: Row(
        mainAxisAlignment: MainAxisAlignment.end,
        children: [
          OutlinedButton(
            onPressed: _closeDialog,
            style: OutlinedButton.styleFrom(
              side: BorderSide(
                  color: _isDark ? AppColors.darkBorderInput : AppColors.lightBorderInput),
              shape: RoundedRectangleBorder(
                  borderRadius: BorderRadius.circular(12)),
              foregroundColor:
                  _isDark ? AppColors.darkTextSecondary : AppColors.lightTextSecondary,
              padding:
                  const EdgeInsets.symmetric(horizontal: 22, vertical: 12),
            ),
            child: const Text('Otkaži',
                style:
                    TextStyle(fontSize: 14, fontWeight: FontWeight.w500)),
          ),
          const SizedBox(width: 12),
          _saving
              ? SizedBox(
                  width: 120,
                  child: Center(
                    child: SizedBox(
                      width: 24,
                      height: 24,
                      child: CircularProgressIndicator(
                        color: _primary,
                        strokeWidth: 2.5,
                      ),
                    ),
                  ),
                )
              : DecoratedBox(
                  decoration: BoxDecoration(
                    gradient: LinearGradient(
                        colors: [_primary, _primaryDark]),
                    borderRadius: BorderRadius.circular(12),
                    boxShadow: [
                      BoxShadow(
                        color: _primary.withValues(alpha: 0.3),
                        blurRadius: 12,
                        offset: const Offset(0, 4),
                      ),
                    ],
                  ),
                  child: Material(
                    color: Colors.transparent,
                    borderRadius: BorderRadius.circular(12),
                    child: InkWell(
                      onTap: _handleSave,
                      borderRadius: BorderRadius.circular(12),
                      child: Padding(
                        padding: const EdgeInsets.symmetric(
                            horizontal: 22, vertical: 12),
                        child: Row(
                          mainAxisSize: MainAxisSize.min,
                          children: [
                            Icon(Icons.save_rounded,
                                size: 18, color: _onPrimary),
                            const SizedBox(width: 8),
                            Text(
                              'Sačuvaj Promjene',
                              style: TextStyle(
                                fontSize: 14,
                                fontWeight: FontWeight.w600,
                                color: _onPrimary,
                              ),
                            ),
                          ],
                        ),
                      ),
                    ),
                  ),
                ),
        ],
      ),
    );
  }

  // ── Helpers ───────────────────────────────────────────────────────────────

  Widget _sectionTitle(String t) => Text(t,
      style: TextStyle(
          fontSize: 20,
          fontWeight: FontWeight.w700,
          color: _isDark ? AppColors.darkTextPrimary : AppColors.lightTextPrimary));

}

// ─── Small helpers ────────────────────────────────────────────────────────────

class _TabDef {
  final String id;
  final String label;
  final IconData icon;
  const _TabDef(this.id, this.label, this.icon);
}

// ── Mode tab button (top row) ─────────────────────────────────────────────────

class _ModeTabBtn extends StatelessWidget {
  final IconData icon;
  final String label;
  final bool active;
  final VoidCallback onTap;

  const _ModeTabBtn(
      {required this.icon,
      required this.label,
      required this.active,
      required this.onTap});

  @override
  Widget build(BuildContext context) {
    final isDark = Theme.of(context).brightness == Brightness.dark;
    final primary = isDark ? AppColors.secondary : AppColors.primary;
    final onPrimary = isDark ? AppColors.darkBackground : Colors.white;
    final inactiveBg = isDark ? AppColors.darkSurface : Colors.white;
    final inactiveBorder = isDark ? AppColors.darkBorderInput : AppColors.lightBorderInput;
    final inactiveText = isDark ? AppColors.darkTextSecondary : AppColors.lightTextSecondary;
    return GestureDetector(
      onTap: onTap,
      child: AnimatedContainer(
        duration: const Duration(milliseconds: 180),
        padding: const EdgeInsets.symmetric(horizontal: 18, vertical: 10),
        decoration: BoxDecoration(
          color: active ? primary : inactiveBg,
          borderRadius: BorderRadius.circular(12),
          border: Border.all(
              color: active ? primary : inactiveBorder),
          boxShadow: active
              ? [
                  BoxShadow(
                    color: primary.withValues(alpha: 0.2),
                    blurRadius: 10,
                    offset: const Offset(0, 4),
                  ),
                ]
              : null,
        ),
        child: Row(
          mainAxisSize: MainAxisSize.min,
          children: [
            Icon(icon,
                size: 18,
                color: active ? onPrimary : inactiveText),
            const SizedBox(width: 8),
            Text(label,
                style: TextStyle(
                  fontSize: 14,
                  fontWeight: FontWeight.w600,
                  color: active ? onPrimary : inactiveText,
                )),
          ],
        ),
      ),
    );
  }
}

// ── Left sidebar tab button ───────────────────────────────────────────────────

class _SideTabBtn extends StatelessWidget {
  final _TabDef tab;
  final bool active;
  final VoidCallback onTap;

  const _SideTabBtn(
      {required this.tab, required this.active, required this.onTap});

  @override
  Widget build(BuildContext context) {
    final isDark = Theme.of(context).brightness == Brightness.dark;
    final primary = isDark ? AppColors.secondary : AppColors.primary;
    final primaryDark = isDark ? AppColors.primary : AppColors.primaryDark;
    final onPrimary = isDark ? AppColors.darkBackground : Colors.white;
    final inactiveIcon = isDark ? AppColors.darkTextTertiary : AppColors.lightTextTertiary;
    final inactiveText = isDark ? AppColors.darkTextSecondary : AppColors.lightTextSecondary;
    return Padding(
      padding: const EdgeInsets.only(bottom: 4),
      child: GestureDetector(
        onTap: onTap,
        child: AnimatedContainer(
          duration: const Duration(milliseconds: 180),
          height: 44,
          decoration: BoxDecoration(
            borderRadius: BorderRadius.circular(10),
            gradient: active
                ? LinearGradient(colors: [primary, primaryDark])
                : null,
            color: active ? null : Colors.transparent,
            boxShadow: active
                ? [
                    BoxShadow(
                      color: primary.withValues(alpha: 0.2),
                      blurRadius: 8,
                      offset: const Offset(0, 3),
                    )
                  ]
                : null,
          ),
          child: Row(
            children: [
              SizedBox(
                width: 46,
                child: Center(
                  child: Icon(tab.icon,
                      size: 18,
                      color: active ? onPrimary : inactiveIcon),
                ),
              ),
              Expanded(
                child: Text(tab.label,
                    overflow: TextOverflow.ellipsis,
                    style: TextStyle(
                      fontSize: 13,
                      fontWeight:
                          active ? FontWeight.w600 : FontWeight.w500,
                      color: active ? onPrimary : inactiveText,
                    )),
              ),
            ],
          ),
        ),
      ),
    );
  }
}

// ── Avatar widget ─────────────────────────────────────────────────────────────

class _Avatar extends StatelessWidget {
  final String initials;
  final double size;

  const _Avatar({required this.initials, required this.size});

  @override
  Widget build(BuildContext context) {
    // Decorative brand accent — kept as the static teal gradient in both
    // themes (see _kPrimary/_kPrimaryDark doc note near the top of the file).
    return Container(
      width: size,
      height: size,
      decoration: const BoxDecoration(
        gradient: LinearGradient(
            begin: Alignment.topLeft,
            end: Alignment.bottomRight,
            colors: [_kPrimary, _kPrimaryDark]),
        shape: BoxShape.circle,
      ),
      alignment: Alignment.center,
      child: Text(initials.toUpperCase(),
          style: TextStyle(
              color: Colors.white,
              fontWeight: FontWeight.w700,
              fontSize: size * 0.36)),
    );
  }
}

// ── Avatar card (display only, no image upload) ──────────────────────────────

class _AvatarCard extends StatelessWidget {
  final String initials;
  final String name;

  const _AvatarCard({required this.initials, required this.name});

  @override
  Widget build(BuildContext context) {
    final isDark = Theme.of(context).brightness == Brightness.dark;
    return Container(
      padding: const EdgeInsets.all(20),
      decoration: BoxDecoration(
        color: isDark ? AppColors.darkSurfaceSubtle : AppColors.lightSurfaceSubtle,
        borderRadius: BorderRadius.circular(14),
        border: Border.all(color: isDark ? AppColors.darkBorder : AppColors.lightBorder),
      ),
      child: Row(
        children: [
          _Avatar(initials: initials, size: 80),
          const SizedBox(width: 20),
          Column(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              Text(name,
                  style: TextStyle(
                      fontSize: 15,
                      fontWeight: FontWeight.w600,
                      color: isDark ? AppColors.darkTextPrimary : AppColors.lightTextPrimary)),
              const SizedBox(height: 4),
              Text('Profilna fotografija',
                  style: TextStyle(
                      fontSize: 12,
                      color: isDark ? AppColors.darkTextTertiary : AppColors.lightTextTertiary)),
            ],
          ),
        ],
      ),
    );
  }
}

// ── Org logo card (with image picker) ────────────────────────────────────────

class _OrgLogoCard extends StatelessWidget {
  final String? logoUrl;
  final Uint8List? logoBytes;
  final VoidCallback onPickLogo;

  const _OrgLogoCard({
    this.logoUrl,
    this.logoBytes,
    required this.onPickLogo,
  });

  bool get _hasLogo => logoBytes != null || (logoUrl != null && logoUrl!.isNotEmpty);

  @override
  Widget build(BuildContext context) {
    final isDark = Theme.of(context).brightness == Brightness.dark;
    final primary = isDark ? AppColors.secondary : AppColors.primary;
    final primaryDark = isDark ? AppColors.primary : AppColors.primaryDark;
    final onPrimary = isDark ? AppColors.darkBackground : Colors.white;
    return Container(
      padding: const EdgeInsets.all(20),
      decoration: BoxDecoration(
        color: isDark ? AppColors.darkSurfaceSubtle : AppColors.lightSurfaceSubtle,
        borderRadius: BorderRadius.circular(14),
        border: Border.all(color: isDark ? AppColors.darkBorder : AppColors.lightBorder),
      ),
      child: Row(
        children: [
          // ── Logo preview ──────────────────────────────────────────────
          Container(
            width: 80,
            height: 80,
            decoration: BoxDecoration(
              shape: BoxShape.circle,
              gradient: _hasLogo
                  ? null
                  : LinearGradient(colors: [primary, primaryDark]),
              image: logoBytes != null
                  ? DecorationImage(
                      image: MemoryImage(logoBytes!), fit: BoxFit.cover)
                  : (logoUrl != null && logoUrl!.isNotEmpty)
                      ? DecorationImage(
                          image: NetworkImage(logoUrl!),
                          fit: BoxFit.cover)
                      : null,
            ),
            child: _hasLogo
                ? null
                : Icon(Icons.business_rounded,
                    color: onPrimary, size: 40),
          ),
          const SizedBox(width: 20),
          // ── Text + button ─────────────────────────────────────────────
          Column(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              Text('Logo Organizacije',
                  style: TextStyle(
                      fontSize: 15,
                      fontWeight: FontWeight.w600,
                      color: isDark ? AppColors.darkTextPrimary : AppColors.lightTextPrimary)),
              const SizedBox(height: 4),
              Text('PNG/JPG, kvadratan (maks. 1MB)',
                  style: TextStyle(
                      fontSize: 12,
                      color: isDark ? AppColors.darkTextTertiary : AppColors.lightTextTertiary)),
              const SizedBox(height: 10),
              OutlinedButton(
                onPressed: onPickLogo,
                style: OutlinedButton.styleFrom(
                  side: BorderSide(
                      color: isDark ? AppColors.darkBorderInput : AppColors.lightBorderInput),
                  shape: RoundedRectangleBorder(
                      borderRadius: BorderRadius.circular(8)),
                  foregroundColor:
                      isDark ? AppColors.darkTextSecondary : AppColors.lightTextSecondary,
                  padding: const EdgeInsets.symmetric(
                      horizontal: 14, vertical: 8),
                  textStyle: const TextStyle(
                      fontSize: 12, fontWeight: FontWeight.w500),
                ),
                child: Text(_hasLogo ? 'Promjeni sliku' : 'Dodaj sliku'),
              ),
            ],
          ),
        ],
      ),
    );
  }
}

// ── Reusable form field ───────────────────────────────────────────────────────

class _Field extends StatefulWidget {
  final String label;
  final TextEditingController controller;
  final String hint;
  final IconData? prefixIcon;
  final bool obscure;
  final bool readOnly;
  final bool fullWidth;
  final int maxLines;
  final TextInputType? keyboardType;

  const _Field({
    required this.label,
    required this.controller,
    this.hint = '',
    this.prefixIcon,
    this.obscure = false,
    this.readOnly = false,
    this.fullWidth = false,
    this.maxLines = 1,
    this.keyboardType,
  });

  @override
  State<_Field> createState() => _FieldState();
}

class _FieldState extends State<_Field> {
  late bool _obscure;

  @override
  void initState() {
    super.initState();
    _obscure = widget.obscure;
  }

  @override
  Widget build(BuildContext context) {
    final isDark = Theme.of(context).brightness == Brightness.dark;
    final primary = isDark ? AppColors.secondary : AppColors.primary;
    final borderColor = isDark ? AppColors.darkBorderInput : AppColors.lightBorderInput;
    final placeholderColor = isDark ? AppColors.darkTextDisabled : AppColors.lightTextDisabled;
    return TextFormField(
      controller: widget.controller,
      obscureText: _obscure,
      readOnly: widget.readOnly,
      maxLines: _obscure ? 1 : widget.maxLines,
      keyboardType: widget.keyboardType,
      style: TextStyle(
        fontSize: 14,
        color: widget.readOnly
            ? (isDark ? AppColors.darkTextTertiary : AppColors.lightTextTertiary)
            : (isDark ? AppColors.darkTextPrimary : AppColors.lightTextPrimary),
      ),
      decoration: InputDecoration(
        labelText: widget.label,
        labelStyle: TextStyle(
            fontSize: 13,
            fontWeight: FontWeight.w500,
            color: isDark ? AppColors.darkTextSecondary : AppColors.lightTextSecondary),
        hintText: widget.hint,
        hintStyle: TextStyle(color: placeholderColor, fontSize: 14),
        filled: true,
        fillColor: widget.readOnly
            ? (isDark ? AppColors.darkSurfaceMuted : AppColors.lightSurfaceMuted)
            : (isDark ? AppColors.darkInputFill : AppColors.lightInputFill),
        prefixIcon: widget.prefixIcon != null
            ? Icon(widget.prefixIcon, color: placeholderColor, size: 18)
            : null,
        suffixIcon: widget.obscure
            ? IconButton(
                icon: Icon(
                    _obscure
                        ? Icons.visibility_off_outlined
                        : Icons.visibility_outlined,
                    color: placeholderColor,
                    size: 18),
                onPressed: () => setState(() => _obscure = !_obscure),
              )
            : null,
        contentPadding:
            const EdgeInsets.symmetric(horizontal: 14, vertical: 12),
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
          borderSide: BorderSide(color: primary, width: 2),
        ),
        disabledBorder: OutlineInputBorder(
          borderRadius: BorderRadius.circular(12),
          borderSide: BorderSide(color: isDark ? AppColors.darkBorder : AppColors.lightBorder),
        ),
      ),
    );
  }
}


// ── Two-column grid ───────────────────────────────────────────────────────────

class _Grid extends StatelessWidget {
  final List<Widget> children;

  const _Grid({required this.children});

  @override
  Widget build(BuildContext context) {
    final cols = <Widget>[];
    for (int i = 0; i < children.length;) {
      final a = children[i];
      final aFull = a is _Field && a.fullWidth;
      if (aFull) {
        cols.add(a);
        cols.add(const SizedBox(height: 14));
        i++;
      } else if (i + 1 < children.length) {
        final b = children[i + 1];
        final bFull = b is _Field && b.fullWidth;
        if (bFull) {
          cols.add(a);
          cols.add(const SizedBox(height: 14));
          i++;
        } else {
          cols.add(Row(
            children: [
              Expanded(child: a),
              const SizedBox(width: 14),
              Expanded(child: b),
            ],
          ));
          cols.add(const SizedBox(height: 14));
          i += 2;
        }
      } else {
        cols.add(Row(
          children: [
            Expanded(child: a),
            const SizedBox(width: 14),
            const Expanded(child: SizedBox()),
          ],
        ));
        cols.add(const SizedBox(height: 14));
        i++;
      }
    }
    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: cols,
    );
  }
}

// ── Toggle row ────────────────────────────────────────────────────────────────

class _ToggleRow extends StatelessWidget {
  final String title;
  final String subtitle;
  final bool value;
  final ValueChanged<bool> onChanged;

  const _ToggleRow({
    required this.title,
    required this.subtitle,
    required this.value,
    required this.onChanged,
  });

  @override
  Widget build(BuildContext context) {
    final isDark = Theme.of(context).brightness == Brightness.dark;
    final primary = isDark ? AppColors.secondary : AppColors.primary;
    return Container(
      padding: const EdgeInsets.all(14),
      decoration: BoxDecoration(
        color: isDark ? AppColors.darkSurfaceSubtle : AppColors.lightSurfaceSubtle,
        borderRadius: BorderRadius.circular(12),
        border: Border.all(color: isDark ? AppColors.darkBorder : AppColors.lightBorder),
      ),
      child: Row(
        children: [
          Expanded(
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Text(title,
                    style: TextStyle(
                        fontSize: 14,
                        fontWeight: FontWeight.w500,
                        color: isDark ? AppColors.darkTextPrimary : AppColors.lightTextPrimary)),
                const SizedBox(height: 2),
                Text(subtitle,
                    style: TextStyle(
                        fontSize: 12,
                        color: isDark ? AppColors.darkTextTertiary : AppColors.lightTextTertiary)),
              ],
            ),
          ),
          const SizedBox(width: 12),
          Switch(
            value: value,
            onChanged: onChanged,
            activeThumbColor: primary,
          ),
        ],
      ),
    );
  }
}

// ── Info banner ───────────────────────────────────────────────────────────────

class _InfoBanner extends StatelessWidget {
  final IconData icon;
  final Color color;
  final Color bg;
  final Color border;
  final String title;
  final String body;

  const _InfoBanner(
      {required this.icon,
      required this.color,
      required this.bg,
      required this.border,
      required this.title,
      required this.body});

  @override
  Widget build(BuildContext context) {
    return Container(
      padding: const EdgeInsets.all(14),
      decoration: BoxDecoration(
        color: bg,
        borderRadius: BorderRadius.circular(12),
        border: Border.all(color: border),
      ),
      child: Row(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Icon(icon, size: 20, color: color),
          const SizedBox(width: 12),
          Expanded(
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Text(title,
                    style: TextStyle(
                        fontSize: 13,
                        fontWeight: FontWeight.w600,
                        color: color)),
                const SizedBox(height: 4),
                Text(body, style: TextStyle(fontSize: 12, color: color)),
              ],
            ),
          ),
        ],
      ),
    );
  }
}

// ── Icon button ───────────────────────────────────────────────────────────────

class _IconBtn extends StatefulWidget {
  final IconData icon;
  final VoidCallback onTap;
  final Color color;
  final Color hoverColor;
  final Color hoverBg;

  const _IconBtn(
      {required this.icon,
      required this.onTap,
      required this.color,
      required this.hoverColor,
      required this.hoverBg});

  @override
  State<_IconBtn> createState() => _IconBtnState();
}

class _IconBtnState extends State<_IconBtn> {
  bool _hovered = false;

  @override
  Widget build(BuildContext context) {
    return MouseRegion(
      cursor: SystemMouseCursors.click,
      onEnter: (_) => setState(() => _hovered = true),
      onExit: (_) => setState(() => _hovered = false),
      child: GestureDetector(
        onTap: widget.onTap,
        child: AnimatedContainer(
          duration: const Duration(milliseconds: 120),
          width: 36,
          height: 36,
          decoration: BoxDecoration(
            color: _hovered ? widget.hoverBg : Colors.transparent,
            borderRadius: BorderRadius.circular(8),
          ),
          child: Icon(widget.icon,
              size: 20,
              color: _hovered ? widget.hoverColor : widget.color),
        ),
      ),
    );
  }
}
