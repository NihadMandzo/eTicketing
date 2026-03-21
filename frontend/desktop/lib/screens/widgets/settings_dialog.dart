import 'package:flutter/material.dart';

import '../../models/responses/user_profile.dart';

// ── Allowed roles that can see the Org tab ────────────────────────────────────
const _kOrgRoles = {'OrganizationSuperAdmin', 'OrganizationAdmin'};
const _kAdminRoles = {'SuperAdmin', 'Admin'};

const Color _kPrimary = Color(0xFF0D7C66);
const Color _kPrimaryDark = Color(0xFF0a6b57);

// ─── Entry point: call this to open the dialog ────────────────────────────────

Future<void> showSettingsDialog(BuildContext context, UserProfile user) {
  return showDialog(
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
  bool _emailNotif = true;
  bool _pushNotif = false;
  bool _eventReminders = true;
  bool _weeklyReports = false;
  String _language = 'bs';
  String _timezone = 'Europe/Sarajevo';

  // ── Org form
  final _orgName = TextEditingController();
  final _orgDesc = TextEditingController();
  final _orgEmail = TextEditingController();
  final _orgPhone = TextEditingController();
  final _orgAddress = TextEditingController();
  final _orgWebsite = TextEditingController();
  bool _orgActive = true;

  bool _saving = false;

  bool get _hasOrg =>
      widget.user.organizationId != null &&
      (_kOrgRoles.contains(widget.user.roleName));

  // ── Profile tabs
  static const _profileTabs = [
    _TabDef('profile', 'Moj Profil', Icons.person_rounded),
    _TabDef('security', 'Sigurnost', Icons.lock_rounded),
    _TabDef('notifications', 'Obavještenja', Icons.notifications_rounded),
    _TabDef('preferences', 'Postavke', Icons.tune_rounded),
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
    _orgName.text = u.organizationName ?? '';
  }

  @override
  void dispose() {
    for (final c in [
      _firstName, _lastName, _username, _email, _phone,
      _currentPwd, _newPwd, _confirmPwd,
      _orgName, _orgDesc, _orgEmail, _orgPhone, _orgAddress, _orgWebsite,
    ]) {
      c.dispose();
    }
    super.dispose();
  }

  // ── Save handler ──────────────────────────────────────────────────────────

  Future<void> _handleSave() async {
    if (_mode == 'security') {
      if (_newPwd.text != _confirmPwd.text) {
        _showError('Lozinke se ne podudaraju.');
        return;
      }
      if (_newPwd.text.isNotEmpty && _currentPwd.text.isEmpty) {
        _showError('Unesite trenutnu lozinku.');
        return;
      }
    }
    setState(() => _saving = true);
    await Future.delayed(const Duration(milliseconds: 600));
    if (!mounted) return;
    setState(() => _saving = false);
    _showSuccess('Promjene su sačuvane.');
  }

  void _showError(String msg) {
    ScaffoldMessenger.of(context).showSnackBar(SnackBar(
      content: Text(msg),
      backgroundColor: const Color(0xFFEF4444),
    ));
  }

  void _showSuccess(String msg) {
    ScaffoldMessenger.of(context).showSnackBar(SnackBar(
      content: Text(msg),
      backgroundColor: _kPrimary,
    ));
  }

  // ── Role display ─────────────────────────────────────────────────────────

  String _roleLabel(String r) {
    switch (r) {
      case 'SuperAdmin': return 'Super Administrator';
      case 'Admin': return 'Administrator';
      case 'OrganizationSuperAdmin': return 'Org. Super Administrator';
      case 'OrganizationAdmin': return 'Org. Administrator';
      default: return r;
    }
  }

  // ─────────────────────────────────────────────────────────────────────────

  @override
  Widget build(BuildContext context) {
    return Dialog(
      backgroundColor: Colors.transparent,
      insetPadding: const EdgeInsets.all(24),
      child: ClipRRect(
        borderRadius: BorderRadius.circular(20),
        child: Container(
          width: 960,
          constraints: BoxConstraints(
            maxHeight: MediaQuery.of(context).size.height * 0.9,
          ),
          color: Colors.white,
          child: Column(
            mainAxisSize: MainAxisSize.min,
            children: [
              _buildHeader(),
              _buildModeSelector(),
              Flexible(
                child: Row(
                  crossAxisAlignment: CrossAxisAlignment.start,
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
    return Container(
      padding: const EdgeInsets.fromLTRB(24, 20, 20, 20),
      decoration: const BoxDecoration(
        gradient: LinearGradient(
          colors: [_kPrimary, _kPrimaryDark],
        ),
      ),
      child: Row(
        children: [
          Container(
            width: 44,
            height: 44,
            decoration: BoxDecoration(
              color: Colors.white.withValues(alpha: 0.2),
              borderRadius: BorderRadius.circular(12),
            ),
            child: Icon(
              isProfile ? Icons.person_rounded : Icons.business_rounded,
              color: Colors.white,
              size: 22,
            ),
          ),
          const SizedBox(width: 14),
          Column(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              Text(
                isProfile ? 'Postavke Profila' : 'Postavke Organizacije',
                style: const TextStyle(
                  fontSize: 22,
                  fontWeight: FontWeight.w700,
                  color: Colors.white,
                ),
              ),
              Text(
                isProfile
                    ? 'Upravljajte svojim nalogom i postavkama'
                    : 'Upravljajte informacijama o organizaciji',
                style: TextStyle(
                  fontSize: 13,
                  color: Colors.white.withValues(alpha: 0.8),
                ),
              ),
            ],
          ),
          const Spacer(),
          _IconBtn(
            icon: Icons.close_rounded,
            onTap: () => Navigator.of(context).pop(),
            color: Colors.white.withValues(alpha: 0.8),
            hoverColor: Colors.white,
            hoverBg: Colors.white.withValues(alpha: 0.1),
          ),
        ],
      ),
    );
  }

  // ── Mode selector ─────────────────────────────────────────────────────────

  Widget _buildModeSelector() {
    return Container(
      padding: const EdgeInsets.symmetric(horizontal: 16, vertical: 10),
      color: const Color(0xFFF9FAFB),
      decoration: const BoxDecoration(
        border: Border(bottom: BorderSide(color: Color(0xFFE5E7EB))),
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
      color: const Color(0xFFF9FAFB),
      decoration: const BoxDecoration(
        border: Border(right: BorderSide(color: Color(0xFFE5E7EB))),
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

    return Container(
      padding: const EdgeInsets.all(14),
      decoration: BoxDecoration(
        color: Colors.white,
        borderRadius: BorderRadius.circular(12),
        border: Border.all(color: const Color(0xFFE5E7EB)),
      ),
      child: isProfile
          ? Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Row(
                  children: [
                    _Avatar(letter: u.firstName.isNotEmpty ? u.firstName[0] : '?', size: 44, circular: true),
                    const SizedBox(width: 10),
                    Expanded(
                      child: Column(
                        crossAxisAlignment: CrossAxisAlignment.start,
                        children: [
                          Text(u.fullName,
                              maxLines: 1,
                              overflow: TextOverflow.ellipsis,
                              style: const TextStyle(
                                fontSize: 13,
                                fontWeight: FontWeight.w600,
                                color: Color(0xFF111827),
                              )),
                          Text(u.email,
                              maxLines: 1,
                              overflow: TextOverflow.ellipsis,
                              style: const TextStyle(
                                fontSize: 11,
                                color: Color(0xFF6B7280),
                              )),
                        ],
                      ),
                    ),
                  ],
                ),
                const SizedBox(height: 10),
                const Divider(height: 1, color: Color(0xFFE5E7EB)),
                const SizedBox(height: 10),
                Container(
                  padding:
                      const EdgeInsets.symmetric(horizontal: 8, vertical: 4),
                  decoration: BoxDecoration(
                    color: _kPrimary.withValues(alpha: 0.1),
                    borderRadius: BorderRadius.circular(8),
                  ),
                  child: Text(
                    _roleLabel(u.roleName),
                    style: const TextStyle(
                      fontSize: 11,
                      fontWeight: FontWeight.w600,
                      color: _kPrimary,
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
                        gradient: const LinearGradient(
                          colors: [_kPrimary, _kPrimaryDark],
                        ),
                        borderRadius: BorderRadius.circular(10),
                      ),
                      child: const Icon(Icons.business_rounded,
                          color: Colors.white, size: 24),
                    ),
                    const SizedBox(width: 10),
                    Expanded(
                      child: Column(
                        crossAxisAlignment: CrossAxisAlignment.start,
                        children: [
                          Text(u.organizationName ?? '-',
                              maxLines: 1,
                              overflow: TextOverflow.ellipsis,
                              style: const TextStyle(
                                fontSize: 13,
                                fontWeight: FontWeight.w600,
                                color: Color(0xFF111827),
                              )),
                          const Text('Organizacija',
                              style: TextStyle(
                                  fontSize: 11, color: Color(0xFF6B7280))),
                        ],
                      ),
                    ),
                  ],
                ),
                const SizedBox(height: 10),
                const Divider(height: 1, color: Color(0xFFE5E7EB)),
                const SizedBox(height: 10),
                Container(
                  padding:
                      const EdgeInsets.symmetric(horizontal: 8, vertical: 4),
                  decoration: BoxDecoration(
                    color: const Color(0xFFDCFCE7),
                    borderRadius: BorderRadius.circular(8),
                  ),
                  child: const Text(
                    'Aktivna',
                    style: TextStyle(
                      fontSize: 11,
                      fontWeight: FontWeight.w600,
                      color: Color(0xFF16A34A),
                    ),
                  ),
                ),
              ],
            ),
    );
  }

  // ── Main content ──────────────────────────────────────────────────────────

  Widget _buildContent() {
    return SingleChildScrollView(
      padding: const EdgeInsets.all(24),
      child: _mode == 'profile'
          ? _buildProfileContent()
          : _buildOrgContent(),
    );
  }

  // ── Profile content ───────────────────────────────────────────────────────

  Widget _buildProfileContent() {
    switch (_profileTab) {
      case 'security':
        return _buildSecurityTab();
      case 'notifications':
        return _buildNotificationsTab();
      case 'preferences':
        return _buildPreferencesTab();
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
        // Avatar card
        _AvatarCard(letter: u.firstName.isNotEmpty ? u.firstName[0] : '?'),
        const SizedBox(height: 20),
        // Fields grid
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
              keyboardType: TextInputType.emailAddress),
          _Field(
              label: 'Broj Telefona',
              controller: _phone,
              hint: '+387 61 123 456',
              prefixIcon: Icons.phone_outlined,
              keyboardType: TextInputType.phone),
          _Field(
              label: 'Uloga',
              controller: TextEditingController(text: _roleLabel(u.roleName)),
              readOnly: true),
          if (u.organizationName != null)
            _Field(
              label: 'Organizacija',
              controller:
                  TextEditingController(text: u.organizationName),
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
          color: const Color(0xFFF59E0B),
          bg: const Color(0xFFFFFBEB),
          border: const Color(0xFFFDE68A),
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
        const SizedBox(height: 28),
        const Divider(color: Color(0xFFE5E7EB)),
        const SizedBox(height: 20),
        _sectionSubtitle('Dvofaktorska Autentifikacija'),
        const SizedBox(height: 12),
        _ToggleRow(
          title: 'Omogući 2FA',
          subtitle: 'Dodajte dodatni sloj sigurnosti vašem nalogu',
          value: false,
          onChanged: (_) {},
        ),
      ],
    );
  }

  Widget _buildNotificationsTab() {
    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        _sectionTitle('Postavke Obavještenja'),
        const SizedBox(height: 16),
        _ToggleRow(
          icon: Icons.mail_outline_rounded,
          iconBg: _kPrimary.withValues(alpha: 0.1),
          iconColor: _kPrimary,
          title: 'Email Obavještenja',
          subtitle: 'Primajte email notifikacije o važnim događajima',
          value: _emailNotif,
          onChanged: (v) => setState(() => _emailNotif = v),
        ),
        const SizedBox(height: 10),
        _ToggleRow(
          icon: Icons.notifications_outlined,
          iconBg: const Color(0xFFCFFAFE),
          iconColor: const Color(0xFF0891B2),
          title: 'Push Obavještenja',
          subtitle: 'Primajte obavještenja na vašem uređaju',
          value: _pushNotif,
          onChanged: (v) => setState(() => _pushNotif = v),
        ),
        const SizedBox(height: 10),
        _ToggleRow(
          icon: Icons.event_rounded,
          iconBg: const Color(0xFFEDE9FE),
          iconColor: const Color(0xFF7C3AED),
          title: 'Podsjetnike za Događaje',
          subtitle: 'Budite obaviješteni o nadolazećim događajima',
          value: _eventReminders,
          onChanged: (v) => setState(() => _eventReminders = v),
        ),
        const SizedBox(height: 10),
        _ToggleRow(
          icon: Icons.bar_chart_rounded,
          iconBg: const Color(0xFFFEF3C7),
          iconColor: const Color(0xFFD97706),
          title: 'Sedmične Izvještaje',
          subtitle: 'Dobijajte sedmične preglede performansi',
          value: _weeklyReports,
          onChanged: (v) => setState(() => _weeklyReports = v),
        ),
      ],
    );
  }

  Widget _buildPreferencesTab() {
    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        _sectionTitle('Opće Postavke'),
        const SizedBox(height: 16),
        _Grid(children: [
          _DropdownField(
            label: 'Jezik',
            value: _language,
            items: const {
              'bs': 'Bosanski',
              'en': 'English',
              'hr': 'Hrvatski',
              'sr': 'Srpski',
            },
            onChanged: (v) => setState(() => _language = v!),
          ),
          _DropdownField(
            label: 'Vremenska Zona',
            value: _timezone,
            items: const {
              'Europe/Sarajevo': 'Europe/Sarajevo (GMT+1)',
              'Europe/Belgrade': 'Europe/Belgrade (GMT+1)',
              'Europe/Zagreb': 'Europe/Zagreb (GMT+1)',
              'UTC': 'UTC (GMT+0)',
            },
            onChanged: (v) => setState(() => _timezone = v!),
          ),
        ]),
        const SizedBox(height: 28),
        const Divider(color: Color(0xFFE5E7EB)),
        const SizedBox(height: 20),
        _sectionSubtitle('Zona Opasnosti'),
        const SizedBox(height: 12),
        Container(
          padding: const EdgeInsets.all(16),
          decoration: BoxDecoration(
            color: const Color(0xFFFEF2F2),
            borderRadius: BorderRadius.circular(12),
            border: Border.all(color: const Color(0xFFFECACA)),
          ),
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              const Text('Obriši Nalog',
                  style: TextStyle(
                    fontSize: 15,
                    fontWeight: FontWeight.w600,
                    color: Color(0xFF991B1B),
                  )),
              const SizedBox(height: 6),
              const Text(
                'Trajno obrišite svoj nalog i sve povezane podatke. '
                'Ova akcija se ne može poništiti.',
                style: TextStyle(fontSize: 13, color: Color(0xFFB91C1C)),
              ),
              const SizedBox(height: 12),
              ElevatedButton(
                onPressed: () {},
                style: ElevatedButton.styleFrom(
                  backgroundColor: const Color(0xFFDC2626),
                  foregroundColor: Colors.white,
                  shape: RoundedRectangleBorder(
                      borderRadius: BorderRadius.circular(10)),
                  padding: const EdgeInsets.symmetric(
                      horizontal: 16, vertical: 10),
                  elevation: 0,
                  textStyle: const TextStyle(
                      fontSize: 13, fontWeight: FontWeight.w600),
                ),
                child: const Text('Obriši Nalog'),
              ),
            ],
          ),
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
        // Org logo card
        _OrgLogoCard(),
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
          color: const Color(0xFF2563EB),
          bg: const Color(0xFFEFF6FF),
          border: const Color(0xFFBFDBFE),
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
      decoration: const BoxDecoration(
        color: Color(0xFFF9FAFB),
        border: Border(top: BorderSide(color: Color(0xFFE5E7EB))),
      ),
      child: Row(
        mainAxisAlignment: MainAxisAlignment.end,
        children: [
          OutlinedButton(
            onPressed: () => Navigator.of(context).pop(),
            style: OutlinedButton.styleFrom(
              side: const BorderSide(color: Color(0xFFD1D5DB)),
              shape: RoundedRectangleBorder(
                  borderRadius: BorderRadius.circular(12)),
              foregroundColor: const Color(0xFF374151),
              padding:
                  const EdgeInsets.symmetric(horizontal: 22, vertical: 12),
            ),
            child: const Text('Otkaži',
                style:
                    TextStyle(fontSize: 14, fontWeight: FontWeight.w500)),
          ),
          const SizedBox(width: 12),
          _saving
              ? const SizedBox(
                  width: 120,
                  child: Center(
                    child: SizedBox(
                      width: 24,
                      height: 24,
                      child: CircularProgressIndicator(
                        color: _kPrimary,
                        strokeWidth: 2.5,
                      ),
                    ),
                  ),
                )
              : DecoratedBox(
                  decoration: BoxDecoration(
                    gradient: const LinearGradient(
                        colors: [_kPrimary, _kPrimaryDark]),
                    borderRadius: BorderRadius.circular(12),
                    boxShadow: [
                      BoxShadow(
                        color: _kPrimary.withValues(alpha: 0.3),
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
                      child: const Padding(
                        padding: EdgeInsets.symmetric(
                            horizontal: 22, vertical: 12),
                        child: Row(
                          mainAxisSize: MainAxisSize.min,
                          children: [
                            Icon(Icons.save_rounded,
                                size: 18, color: Colors.white),
                            SizedBox(width: 8),
                            Text(
                              'Sačuvaj Promjene',
                              style: TextStyle(
                                fontSize: 14,
                                fontWeight: FontWeight.w600,
                                color: Colors.white,
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
      style: const TextStyle(
          fontSize: 20,
          fontWeight: FontWeight.w700,
          color: Color(0xFF111827)));

  Widget _sectionSubtitle(String t) => Text(t,
      style: const TextStyle(
          fontSize: 16,
          fontWeight: FontWeight.w700,
          color: Color(0xFF111827)));
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
    return GestureDetector(
      onTap: onTap,
      child: AnimatedContainer(
        duration: const Duration(milliseconds: 180),
        padding: const EdgeInsets.symmetric(horizontal: 18, vertical: 10),
        decoration: BoxDecoration(
          color: active ? _kPrimary : Colors.white,
          borderRadius: BorderRadius.circular(12),
          border: Border.all(
              color: active ? _kPrimary : const Color(0xFFD1D5DB)),
          boxShadow: active
              ? [
                  BoxShadow(
                    color: _kPrimary.withValues(alpha: 0.2),
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
                size: 18, color: active ? Colors.white : const Color(0xFF374151)),
            const SizedBox(width: 8),
            Text(label,
                style: TextStyle(
                  fontSize: 14,
                  fontWeight: FontWeight.w600,
                  color: active ? Colors.white : const Color(0xFF374151),
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
                ? const LinearGradient(colors: [_kPrimary, _kPrimaryDark])
                : null,
            color: active ? null : Colors.transparent,
            boxShadow: active
                ? [
                    BoxShadow(
                      color: _kPrimary.withValues(alpha: 0.2),
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
                      color: active
                          ? Colors.white
                          : const Color(0xFF6B7280)),
                ),
              ),
              Expanded(
                child: Text(tab.label,
                    overflow: TextOverflow.ellipsis,
                    style: TextStyle(
                      fontSize: 13,
                      fontWeight:
                          active ? FontWeight.w600 : FontWeight.w500,
                      color:
                          active ? Colors.white : const Color(0xFF374151),
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
  final String letter;
  final double size;
  final bool circular;

  const _Avatar(
      {required this.letter, required this.size, this.circular = false});

  @override
  Widget build(BuildContext context) {
    return Container(
      width: size,
      height: size,
      decoration: BoxDecoration(
        gradient: const LinearGradient(
            begin: Alignment.topLeft,
            end: Alignment.bottomRight,
            colors: [_kPrimary, _kPrimaryDark]),
        borderRadius:
            circular ? BorderRadius.circular(size) : BorderRadius.circular(size * 0.25),
      ),
      alignment: Alignment.center,
      child: Text(letter.toUpperCase(),
          style: TextStyle(
              color: Colors.white,
              fontWeight: FontWeight.w700,
              fontSize: size * 0.4)),
    );
  }
}

// ── Avatar card with change button ───────────────────────────────────────────

class _AvatarCard extends StatelessWidget {
  final String letter;

  const _AvatarCard({required this.letter});

  @override
  Widget build(BuildContext context) {
    return Container(
      padding: const EdgeInsets.all(20),
      decoration: BoxDecoration(
        color: const Color(0xFFF9FAFB),
        borderRadius: BorderRadius.circular(14),
        border: Border.all(color: const Color(0xFFE5E7EB)),
      ),
      child: Row(
        children: [
          Stack(
            children: [
              _Avatar(letter: letter, size: 80, circular: true),
              Positioned(
                bottom: 0,
                right: 0,
                child: Container(
                  width: 28,
                  height: 28,
                  decoration: BoxDecoration(
                    color: Colors.white,
                    shape: BoxShape.circle,
                    border: Border.all(
                        color: const Color(0xFFE5E7EB), width: 2),
                    boxShadow: [
                      BoxShadow(
                          color: Colors.black.withValues(alpha: 0.08),
                          blurRadius: 8)
                    ],
                  ),
                  child: const Icon(Icons.camera_alt_rounded,
                      size: 14, color: Color(0xFF374151)),
                ),
              ),
            ],
          ),
          const SizedBox(width: 20),
          Column(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              const Text('Profilna Slika',
                  style: TextStyle(
                      fontSize: 14,
                      fontWeight: FontWeight.w600,
                      color: Color(0xFF111827))),
              const SizedBox(height: 4),
              const Text('PNG ili JPG (maks. 2MB)',
                  style: TextStyle(
                      fontSize: 12, color: Color(0xFF6B7280))),
              const SizedBox(height: 10),
              OutlinedButton(
                onPressed: () {},
                style: OutlinedButton.styleFrom(
                  side:
                      const BorderSide(color: Color(0xFFD1D5DB)),
                  shape: RoundedRectangleBorder(
                      borderRadius: BorderRadius.circular(8)),
                  foregroundColor: const Color(0xFF374151),
                  padding: const EdgeInsets.symmetric(
                      horizontal: 14, vertical: 8),
                  textStyle: const TextStyle(
                      fontSize: 12, fontWeight: FontWeight.w500),
                ),
                child: const Text('Promijeni Sliku'),
              ),
            ],
          ),
        ],
      ),
    );
  }
}

// ── Org logo card ──────────────────────────────────────────────────────────

class _OrgLogoCard extends StatelessWidget {
  const _OrgLogoCard();

  @override
  Widget build(BuildContext context) {
    return Container(
      padding: const EdgeInsets.all(20),
      decoration: BoxDecoration(
        color: const Color(0xFFF9FAFB),
        borderRadius: BorderRadius.circular(14),
        border: Border.all(color: const Color(0xFFE5E7EB)),
      ),
      child: Row(
        children: [
          Stack(
            children: [
              Container(
                width: 80,
                height: 80,
                decoration: BoxDecoration(
                  gradient: const LinearGradient(
                      colors: [_kPrimary, _kPrimaryDark]),
                  borderRadius: BorderRadius.circular(16),
                ),
                child: const Icon(Icons.business_rounded,
                    color: Colors.white, size: 40),
              ),
              Positioned(
                bottom: 0,
                right: 0,
                child: Container(
                  width: 28,
                  height: 28,
                  decoration: BoxDecoration(
                    color: Colors.white,
                    shape: BoxShape.circle,
                    border: Border.all(
                        color: const Color(0xFFE5E7EB), width: 2),
                  ),
                  child: const Icon(Icons.camera_alt_rounded,
                      size: 14, color: Color(0xFF374151)),
                ),
              ),
            ],
          ),
          const SizedBox(width: 20),
          Column(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              const Text('Logo Organizacije',
                  style: TextStyle(
                      fontSize: 14,
                      fontWeight: FontWeight.w600,
                      color: Color(0xFF111827))),
              const SizedBox(height: 4),
              const Text('PNG ili SVG (maks. 2MB)',
                  style: TextStyle(
                      fontSize: 12, color: Color(0xFF6B7280))),
              const SizedBox(height: 10),
              OutlinedButton(
                onPressed: () {},
                style: OutlinedButton.styleFrom(
                  side: const BorderSide(color: Color(0xFFD1D5DB)),
                  shape: RoundedRectangleBorder(
                      borderRadius: BorderRadius.circular(8)),
                  foregroundColor: const Color(0xFF374151),
                  padding: const EdgeInsets.symmetric(
                      horizontal: 14, vertical: 8),
                  textStyle: const TextStyle(
                      fontSize: 12, fontWeight: FontWeight.w500),
                ),
                child: const Text('Promijeni Logo'),
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
    return TextFormField(
      controller: widget.controller,
      obscureText: _obscure,
      readOnly: widget.readOnly,
      maxLines: _obscure ? 1 : widget.maxLines,
      keyboardType: widget.keyboardType,
      style: TextStyle(
        fontSize: 14,
        color: widget.readOnly
            ? const Color(0xFF6B7280)
            : const Color(0xFF111827),
      ),
      decoration: InputDecoration(
        labelText: widget.label,
        labelStyle: const TextStyle(
            fontSize: 13,
            fontWeight: FontWeight.w500,
            color: Color(0xFF374151)),
        hintText: widget.hint,
        hintStyle:
            const TextStyle(color: Color(0xFF9CA3AF), fontSize: 14),
        filled: true,
        fillColor: widget.readOnly
            ? const Color(0xFFF3F4F6)
            : const Color(0xFFFAFAFA),
        prefixIcon: widget.prefixIcon != null
            ? Icon(widget.prefixIcon,
                color: const Color(0xFF9CA3AF), size: 18)
            : null,
        suffixIcon: widget.obscure
            ? IconButton(
                icon: Icon(
                    _obscure
                        ? Icons.visibility_off_outlined
                        : Icons.visibility_outlined,
                    color: const Color(0xFF9CA3AF),
                    size: 18),
                onPressed: () => setState(() => _obscure = !_obscure),
              )
            : null,
        contentPadding:
            const EdgeInsets.symmetric(horizontal: 14, vertical: 12),
        border: OutlineInputBorder(
          borderRadius: BorderRadius.circular(12),
          borderSide: const BorderSide(color: Color(0xFFD1D5DB)),
        ),
        enabledBorder: OutlineInputBorder(
          borderRadius: BorderRadius.circular(12),
          borderSide: const BorderSide(color: Color(0xFFD1D5DB)),
        ),
        focusedBorder: OutlineInputBorder(
          borderRadius: BorderRadius.circular(12),
          borderSide: const BorderSide(color: _kPrimary, width: 2),
        ),
        disabledBorder: OutlineInputBorder(
          borderRadius: BorderRadius.circular(12),
          borderSide: const BorderSide(color: Color(0xFFE5E7EB)),
        ),
      ),
    );
  }
}

// ── Dropdown field ────────────────────────────────────────────────────────────

class _DropdownField extends StatelessWidget {
  final String label;
  final String value;
  final Map<String, String> items;
  final ValueChanged<String?> onChanged;

  const _DropdownField(
      {required this.label,
      required this.value,
      required this.items,
      required this.onChanged});

  @override
  Widget build(BuildContext context) {
    return DropdownButtonFormField<String>(
      value: value,
      onChanged: onChanged,
      style: const TextStyle(fontSize: 14, color: Color(0xFF111827)),
      decoration: InputDecoration(
        labelText: label,
        labelStyle: const TextStyle(
            fontSize: 13,
            fontWeight: FontWeight.w500,
            color: Color(0xFF374151)),
        filled: true,
        fillColor: const Color(0xFFFAFAFA),
        contentPadding:
            const EdgeInsets.symmetric(horizontal: 14, vertical: 12),
        border: OutlineInputBorder(
          borderRadius: BorderRadius.circular(12),
          borderSide: const BorderSide(color: Color(0xFFD1D5DB)),
        ),
        enabledBorder: OutlineInputBorder(
          borderRadius: BorderRadius.circular(12),
          borderSide: const BorderSide(color: Color(0xFFD1D5DB)),
        ),
        focusedBorder: OutlineInputBorder(
          borderRadius: BorderRadius.circular(12),
          borderSide: const BorderSide(color: _kPrimary, width: 2),
        ),
      ),
      items: items.entries
          .map((e) => DropdownMenuItem(value: e.key, child: Text(e.value)))
          .toList(),
    );
  }
}

// ── Two-column grid ───────────────────────────────────────────────────────────

class _Grid extends StatelessWidget {
  final List<Widget> children;

  const _Grid({required this.children});

  @override
  Widget build(BuildContext context) {
    // We wrap each field — fullWidth ones span both columns using a trick
    // by building pairs manually.
    final cols = <Widget>[];
    for (int i = 0; i < children.length;) {
      final a = children[i];
      final aFull = a is _Field && a.fullWidth ||
          a is _DropdownField && false;
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
          children: [Expanded(child: a), const SizedBox(width: 14), const Expanded(child: SizedBox())],
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
  final IconData? icon;
  final Color? iconBg;
  final Color? iconColor;
  final String title;
  final String subtitle;
  final bool value;
  final ValueChanged<bool> onChanged;

  const _ToggleRow({
    this.icon,
    this.iconBg,
    this.iconColor,
    required this.title,
    required this.subtitle,
    required this.value,
    required this.onChanged,
  });

  @override
  Widget build(BuildContext context) {
    return Container(
      padding: const EdgeInsets.all(14),
      decoration: BoxDecoration(
        color: const Color(0xFFF9FAFB),
        borderRadius: BorderRadius.circular(12),
        border: Border.all(color: const Color(0xFFE5E7EB)),
      ),
      child: Row(
        children: [
          if (icon != null) ...[
            Container(
              width: 40,
              height: 40,
              decoration: BoxDecoration(
                color: iconBg,
                borderRadius: BorderRadius.circular(10),
              ),
              child: Icon(icon, size: 20, color: iconColor),
            ),
            const SizedBox(width: 14),
          ],
          Expanded(
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Text(title,
                    style: const TextStyle(
                        fontSize: 14,
                        fontWeight: FontWeight.w500,
                        color: Color(0xFF111827))),
                const SizedBox(height: 2),
                Text(subtitle,
                    style: const TextStyle(
                        fontSize: 12, color: Color(0xFF6B7280))),
              ],
            ),
          ),
          const SizedBox(width: 12),
          Switch(
            value: value,
            onChanged: onChanged,
            activeColor: _kPrimary,
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
                Text(body,
                    style: TextStyle(fontSize: 12, color: color)),
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
