import 'package:flutter/material.dart';

import '../core/session.dart';
import '../models/responses/user_response.dart';
import '../theme/app_colors.dart';
import '../widgets/initials_avatar.dart';
import 'events_screen.dart';
import 'my_tickets_screen.dart';
import 'profile_screen.dart';
import 'validation_products_screen.dart';

/// The signed-in app shell — matches the design mockup's screens 2/5/8
/// exactly: no system title bar on any of the root tabs (each manages its
/// own top content, or none — Profil has none at all), and a custom bottom
/// nav bar styled to the mockup's `.kbottomnav`/`.knavitem` CSS rather than
/// Material's default `NavigationBar` chrome.
///
/// Događaji/Moje ulaznice show the mockup's own header treatment (brand
/// row / plain title) wrapping the real [EventsScreen]/[MyTicketsScreen]
/// content now that Catalog/Ticketing are wired into mobile.
///
/// **The tab set is role-dependent.** Buyers see the mockup's three tabs
/// unchanged. Organization staff — and platform staff, who override
/// everything else in this system too — get a fourth, "Validacija", for
/// scanning tickets at the gate. That tab is the only part of this app aimed
/// at someone working an event rather than attending one, which is why it's
/// appended rather than folded into Profil.
class MainShell extends StatefulWidget {
  /// Which tab to land on — defaults to Događaji, but PaymentScreen lands
  /// on Moje ulaznice (index 1) right after a successful purchase.
  final int initialIndex;

  const MainShell({super.key, this.initialIndex = 0});

  @override
  State<MainShell> createState() => _MainShellState();
}

/// Roles allowed to validate tickets, mirroring the backend's "Organizer"
/// policy (see AuthorizationPolicyExtensions — it admits platform staff too,
/// and TicketValidationService then skips the per-organization ownership
/// check for them). The tab is a convenience, never the security boundary:
/// the endpoints reject a wrong role regardless of what this app renders.
const _validationRoles = {
  'OrganizationSuperAdmin',
  'OrganizationAdmin',
  'Admin',
  'SuperAdmin',
};

class _MainShellState extends State<MainShell> {
  late int _index = widget.initialIndex;

  void _goToProfile() => setState(() => _index = 2);

  @override
  Widget build(BuildContext context) {
    return ValueListenableBuilder<UserResponse?>(
      valueListenable: Session.currentUser,
      builder: (context, user, _) {
        final canValidate = user != null && _validationRoles.contains(user.roleName);

        final tabs = <Widget>[
          _EventsTab(onAvatarTap: _goToProfile),
          const _TicketsTab(),
          const ProfileScreen(),
          if (canValidate) const _ValidationTab(),
        ];

        // Signing out mid-session shrinks the tab list under us; clamp so the
        // shell can't be left pointing at an index that no longer exists.
        final safeIndex = _index.clamp(0, tabs.length - 1);

        return Scaffold(
          body: SafeArea(
            bottom: false,
            child: IndexedStack(index: safeIndex, children: tabs),
          ),
          bottomNavigationBar: _KudaBottomNav(
            index: safeIndex,
            showValidation: canValidate,
            onChanged: (i) => setState(() => _index = i),
          ),
        );
      },
    );
  }
}

/// Custom bottom nav — mirrors `.kbottomnav`/`.knavitem` from the mockup
/// pixel-for-pixel rather than using Material's `NavigationBar` (different
/// height, background, indicator style, and label treatment).
class _KudaBottomNav extends StatelessWidget {
  final int index;
  final bool showValidation;
  final ValueChanged<int> onChanged;

  const _KudaBottomNav({
    required this.index,
    required this.showValidation,
    required this.onChanged,
  });

  static const _baseItems = [
    // The mockup's "Događaji" nav icon is literally a house shape, not an
    // event/calendar glyph — matched exactly rather than substituted.
    (icon: Icons.home_outlined, activeIcon: Icons.home_rounded, label: 'Događaji'),
    (icon: Icons.confirmation_number_outlined, activeIcon: Icons.confirmation_number_rounded, label: 'Moje ulaznice'),
    (icon: Icons.person_outline_rounded, activeIcon: Icons.person_rounded, label: 'Profil'),
  ];

  static const _validationItem = (
    icon: Icons.qr_code_scanner_outlined,
    activeIcon: Icons.qr_code_scanner_rounded,
    label: 'Validacija',
  );

  @override
  Widget build(BuildContext context) {
    final isDark = Theme.of(context).brightness == Brightness.dark;
    final primary = Theme.of(context).colorScheme.primary;
    final inactive = isDark ? AppColors.darkTextTertiary : AppColors.lightTextTertiary;
    final border = isDark ? AppColors.darkBorder : AppColors.lightBorder;
    final background = (isDark ? AppColors.darkSurface : AppColors.lightSurface).withValues(alpha: 0.92);

    final items = [..._baseItems, if (showValidation) _validationItem];

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
            for (var i = 0; i < items.length; i++)
              // Expanded so four labels share the width evenly instead of the
              // longest ("Moje ulaznice") pushing the row into an overflow at
              // ~320px, which is the narrowest width this app must render at.
              Expanded(
                child: _NavItem(
                  icon: i == index ? items[i].activeIcon : items[i].icon,
                  label: items[i].label,
                  color: i == index ? primary : inactive,
                  onTap: () => onChanged(i),
                ),
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
        padding: const EdgeInsets.symmetric(horizontal: 4),
        child: Column(
          mainAxisSize: MainAxisSize.min,
          children: [
            Icon(icon, size: 22, color: color),
            const SizedBox(height: 3),
            Text(
              label,
              maxLines: 1,
              overflow: TextOverflow.ellipsis,
              textAlign: TextAlign.center,
              style: TextStyle(fontSize: 11, fontWeight: FontWeight.w600, color: color),
            ),
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
    return Column(
      children: [
        Padding(
          padding: const EdgeInsets.fromLTRB(20, 12, 20, 16),
          child: Row(
            children: [
              Image.asset('assets/logo.png', height: 28),
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
        const Expanded(child: EventsScreen()),
      ],
    );
  }
}

/// "Moje ulaznice" tab — the mockup's plain page-title header (no brand
/// row, no system app bar).
class _TicketsTab extends StatelessWidget {
  const _TicketsTab();

  @override
  Widget build(BuildContext context) => const _TitledTab(title: 'Moje ulaznice', child: MyTicketsScreen());
}

/// "Validacija" tab — organizer-only, same plain-title header treatment as
/// Moje ulaznice so the added tab doesn't introduce a second convention.
class _ValidationTab extends StatelessWidget {
  const _ValidationTab();

  @override
  Widget build(BuildContext context) => const _TitledTab(title: 'Validacija ulaznica', child: ValidationProductsScreen());
}

class _TitledTab extends StatelessWidget {
  final String title;
  final Widget child;

  const _TitledTab({required this.title, required this.child});

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
              title,
              style: TextStyle(
                fontSize: 20,
                fontWeight: FontWeight.w700,
                color: isDark ? AppColors.darkTextPrimary : AppColors.lightTextPrimary,
              ),
            ),
          ),
        ),
        Expanded(child: child),
      ],
    );
  }
}
