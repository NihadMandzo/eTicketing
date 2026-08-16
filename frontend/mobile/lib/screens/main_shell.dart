import 'package:flutter/material.dart';

import '../core/session.dart';
import '../models/responses/user_response.dart';
import '../theme/app_colors.dart';
import '../widgets/initials_avatar.dart';
import 'profile_screen.dart';

/// The signed-in app shell — matches the design mockup's screens 2/5/8
/// exactly: no system title bar on any of the 3 root tabs (each manages its
/// own top content, or none — Profil has none at all), and a custom bottom
/// nav bar styled to the mockup's `.kbottomnav`/`.knavitem` CSS rather than
/// Material's default `NavigationBar` chrome.
///
/// Događaji/Moje ulaznice show the mockup's own header treatment (brand
/// row / plain title) but a "uskoro dostupno" body — Catalog/Ticketing
/// aren't wired into mobile yet (see the "Design reference" note in
/// `.claude/rules/22-frontend-mobile.md`), so this matches the mockup's
/// structure without fabricating event/ticket data.
class MainShell extends StatefulWidget {
  const MainShell({super.key});

  @override
  State<MainShell> createState() => _MainShellState();
}

class _MainShellState extends State<MainShell> {
  int _index = 0;

  void _goToProfile() => setState(() => _index = 2);

  @override
  Widget build(BuildContext context) {
    final tabs = [
      _EventsTab(onAvatarTap: _goToProfile),
      const _TicketsTab(),
      const ProfileScreen(),
    ];

    return Scaffold(
      body: SafeArea(
        bottom: false,
        child: IndexedStack(index: _index, children: tabs),
      ),
      bottomNavigationBar: _KudaBottomNav(
        index: _index,
        onChanged: (i) => setState(() => _index = i),
      ),
    );
  }
}

/// Custom bottom nav — mirrors `.kbottomnav`/`.knavitem` from the mockup
/// pixel-for-pixel rather than using Material's `NavigationBar` (different
/// height, background, indicator style, and label treatment).
class _KudaBottomNav extends StatelessWidget {
  final int index;
  final ValueChanged<int> onChanged;

  const _KudaBottomNav({required this.index, required this.onChanged});

  static const _items = [
    // The mockup's "Događaji" nav icon is literally a house shape, not an
    // event/calendar glyph — matched exactly rather than substituted.
    (icon: Icons.home_outlined, activeIcon: Icons.home_rounded, label: 'Događaji'),
    (icon: Icons.confirmation_number_outlined, activeIcon: Icons.confirmation_number_rounded, label: 'Moje ulaznice'),
    (icon: Icons.person_outline_rounded, activeIcon: Icons.person_rounded, label: 'Profil'),
  ];

  @override
  Widget build(BuildContext context) {
    final isDark = Theme.of(context).brightness == Brightness.dark;
    final primary = Theme.of(context).colorScheme.primary;
    final inactive = isDark ? AppColors.darkTextTertiary : AppColors.lightTextTertiary;
    final border = isDark ? AppColors.darkBorder : AppColors.lightBorder;
    final background = (isDark ? AppColors.darkSurface : AppColors.lightSurface).withValues(alpha: 0.92);

    return SafeArea(
      top: false,
      child: Container(
        height: 66,
        decoration: BoxDecoration(
          color: background,
          border: Border(top: BorderSide(color: border)),
        ),
        child: Row(
          mainAxisAlignment: MainAxisAlignment.spaceAround,
          children: [
            for (var i = 0; i < _items.length; i++)
              _NavItem(
                icon: i == index ? _items[i].activeIcon : _items[i].icon,
                label: _items[i].label,
                color: i == index ? primary : inactive,
                onTap: () => onChanged(i),
              ),
          ],
        ),
      ),
    );
  }
}

class _NavItem extends StatelessWidget {
  final IconData icon;
  final String label;
  final Color color;
  final VoidCallback onTap;

  const _NavItem({required this.icon, required this.label, required this.color, required this.onTap});

  @override
  Widget build(BuildContext context) {
    return InkWell(
      onTap: onTap,
      child: Padding(
        padding: const EdgeInsets.symmetric(horizontal: 12),
        child: Column(
          mainAxisSize: MainAxisSize.min,
          children: [
            Icon(icon, size: 22, color: color),
            const SizedBox(height: 3),
            Text(label, style: TextStyle(fontSize: 11, fontWeight: FontWeight.w600, color: color)),
          ],
        ),
      ),
    );
  }
}

/// "Događaji" tab — the mockup's own brand-row header (logo + avatar), not
/// a system app bar. Tapping the avatar jumps to the Profil tab, same as
/// tapping it would in the mockup.
class _EventsTab extends StatelessWidget {
  final VoidCallback onAvatarTap;

  const _EventsTab({required this.onAvatarTap});

  @override
  Widget build(BuildContext context) {
    final scheme = Theme.of(context).colorScheme;
    final isDark = Theme.of(context).brightness == Brightness.dark;

    return Column(
      children: [
        Padding(
          padding: const EdgeInsets.fromLTRB(20, 12, 20, 16),
          child: Row(
            children: [
              Icon(Icons.confirmation_number_rounded, size: 24, color: scheme.primary),
              const SizedBox(width: 8),
              Text(
                'eKarta',
                style: TextStyle(
                  fontSize: 18,
                  fontWeight: FontWeight.w700,
                  color: isDark ? AppColors.darkTextPrimary : AppColors.lightTextPrimary,
                ),
              ),
              const Spacer(),
              ValueListenableBuilder<UserResponse?>(
                valueListenable: Session.currentUser,
                builder: (context, user, _) {
                  if (user == null) return const SizedBox.shrink();
                  return InitialsAvatar(name: user.fullName, radius: 17, onTap: onAvatarTap);
                },
              ),
            ],
          ),
        ),
        const Expanded(
          child: _ComingSoonBody(
            icon: Icons.event_outlined,
            message: 'Pregled događaja će uskoro biti dostupan.',
          ),
        ),
      ],
    );
  }
}

/// "Moje ulaznice" tab — the mockup's plain page-title header (no brand
/// row, no system app bar).
class _TicketsTab extends StatelessWidget {
  const _TicketsTab();

  @override
  Widget build(BuildContext context) {
    final isDark = Theme.of(context).brightness == Brightness.dark;

    return Column(
      children: [
        Padding(
          padding: const EdgeInsets.fromLTRB(20, 12, 20, 4),
          child: Align(
            alignment: Alignment.centerLeft,
            child: Text(
              'Moje ulaznice',
              style: TextStyle(
                fontSize: 20,
                fontWeight: FontWeight.w700,
                color: isDark ? AppColors.darkTextPrimary : AppColors.lightTextPrimary,
              ),
            ),
          ),
        ),
        const Expanded(
          child: _ComingSoonBody(
            icon: Icons.confirmation_number_outlined,
            message: 'Vaše kupljene ulaznice će uskoro biti dostupne ovdje.',
          ),
        ),
      ],
    );
  }
}

class _ComingSoonBody extends StatelessWidget {
  final IconData icon;
  final String message;

  const _ComingSoonBody({required this.icon, required this.message});

  @override
  Widget build(BuildContext context) {
    final isDark = Theme.of(context).brightness == Brightness.dark;
    final tertiaryText = isDark ? AppColors.darkTextTertiary : AppColors.lightTextTertiary;
    final scheme = Theme.of(context).colorScheme;

    return Center(
      child: Padding(
        padding: const EdgeInsets.all(32),
        child: Column(
          mainAxisSize: MainAxisSize.min,
          children: [
            Icon(icon, size: 48, color: scheme.primary),
            const SizedBox(height: 12),
            Text(message, textAlign: TextAlign.center, style: TextStyle(fontSize: 14, color: tertiaryText)),
          ],
        ),
      ),
    );
  }
}
