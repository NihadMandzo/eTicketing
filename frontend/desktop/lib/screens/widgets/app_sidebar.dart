import 'package:flutter/material.dart';

import '../../models/responses/user_profile.dart';
import '../../theme/app_colors.dart';

// ─── Nav item model ───────────────────────────────────────────────────────────

class NavItem {
  final String id;
  final String label;
  final IconData icon;
  final List<String> allowedRoles; // empty = visible to everyone

  const NavItem({
    required this.id,
    required this.label,
    required this.icon,
    this.allowedRoles = const [],
  });
}

// ─── Nav items ────────────────────────────────────────────────────────────────

const List<NavItem> kNavItems = [
  NavItem(
    id: 'dashboard',
    label: 'Kontrolna tabla',
    icon: Icons.dashboard_rounded,
  ),
  NavItem(
    id: 'products',
    label: 'Proizvodi',
    icon: Icons.inventory_2_rounded,
    allowedRoles: ['OrganizationAdmin', 'OrganizationSuperAdmin'],
  ),
  NavItem(
    id: 'categories',
    label: 'Kategorije',
    icon: Icons.category_rounded,
    allowedRoles: ['Admin', 'SuperAdmin'],
  ),
  NavItem(
    id: 'tickets',
    label: 'Karte',
    icon: Icons.confirmation_number_rounded,
    allowedRoles: ['OrganizationAdmin', 'OrganizationSuperAdmin'],
  ),
  NavItem(
    id: 'gate-devices',
    label: 'Ulazni uređaji',
    icon: Icons.sensor_door_rounded,
    // PlatformStaff too: they hold the same override over every organization's devices as they do
    // over its products/sectors/tickets (see .claude/rules/01-domain.md).
    allowedRoles: ['OrganizationAdmin', 'OrganizationSuperAdmin', 'Admin', 'SuperAdmin'],
  ),
  NavItem(
    id: 'organizations',
    label: 'Organizacije',
    icon: Icons.business_rounded,
    allowedRoles: ['Admin', 'SuperAdmin'],
  ),
  NavItem(
    id: 'users',
    label: 'Korisnici',
    icon: Icons.people_rounded,
    allowedRoles: ['SuperAdmin', 'OrganizationSuperAdmin'],
  ),
  NavItem(
    id: 'recommendations',
    label: 'Preporuke',
    icon: Icons.auto_awesome_rounded,
    allowedRoles: ['Admin', 'SuperAdmin'],
  ),
  NavItem(
    id: 'reports',
    label: 'Izvještaji',
    icon: Icons.bar_chart_rounded,
    allowedRoles: ['Admin', 'SuperAdmin', 'OrganizationAdmin', 'OrganizationSuperAdmin'],
  ),
];

// ─── Constants ────────────────────────────────────────────────────────────────

// The sidebar is a fixed-width icon-only rail — it never expands into a
// drawer. Hovering a nav item surfaces its label via a Tooltip instead (see
// _NavButton) rather than growing the whole shell.
const double _kWidth = 72.0;

// ─── AppSidebar ───────────────────────────────────────────────────────────────

class AppSidebar extends StatelessWidget {
  final UserProfile user;
  final String currentPage;
  final ValueChanged<String> onPageChange;
  final VoidCallback onSettings;

  const AppSidebar({
    super.key,
    required this.user,
    required this.currentPage,
    required this.onPageChange,
    required this.onSettings,
  });

  List<NavItem> get _filtered => kNavItems
      .where((i) => i.allowedRoles.isEmpty || i.allowedRoles.contains(user.roleName))
      .toList();

  @override
  Widget build(BuildContext context) {
    return _SidebarShell(
      child: Column(
        children: [
          const _Logo(),
          Expanded(
            child: _NavList(
              items: _filtered,
              currentPage: currentPage,
              onPageChange: onPageChange,
            ),
          ),
          _BottomActions(onSettings: onSettings),
        ],
      ),
    );
  }
}

// ─── Sidebar shell container ──────────────────────────────────────────────────

class _SidebarShell extends StatelessWidget {
  final Widget child;

  const _SidebarShell({required this.child});

  @override
  Widget build(BuildContext context) {
    final isDark = Theme.of(context).brightness == Brightness.dark;
    return SizedBox(
      width: _kWidth,
      child: DecoratedBox(
        decoration: BoxDecoration(
          color: isDark ? AppColors.darkSurface : Colors.white,
          border: Border(
            right: BorderSide(
              color: isDark ? AppColors.darkBorder : AppColors.lightBorder,
            ),
          ),
          boxShadow: isDark
              ? null
              : const [
                  BoxShadow(
                    color: Color(0x0C000000),
                    blurRadius: 16,
                    offset: Offset(4, 0),
                  ),
                ],
        ),
        child: child,
      ),
    );
  }
}

// ─── Logo ─────────────────────────────────────────────────────────────────────

class _Logo extends StatelessWidget {
  const _Logo();

  @override
  Widget build(BuildContext context) {
    final isDark = Theme.of(context).brightness == Brightness.dark;
    return Container(
      height: 64,
      alignment: Alignment.center,
      decoration: BoxDecoration(
        border: Border(
          bottom: BorderSide(
            color: isDark ? AppColors.darkBorder : AppColors.lightBorder,
          ),
        ),
      ),
      child: Tooltip(
        message: 'eKarta Manager',
        waitDuration: const Duration(milliseconds: 600),
        child: SizedBox(
          width: 40,
          height: 40,
          child: ClipRRect(
            borderRadius: BorderRadius.circular(8),
            child: Image.asset(
              'assets/logo.png',
              fit: BoxFit.contain,
            ),
          ),
        ),
      ),
    );
  }
}

// ─── Nav list ─────────────────────────────────────────────────────────────────

class _NavList extends StatelessWidget {
  final List<NavItem> items;
  final String currentPage;
  final ValueChanged<String> onPageChange;

  const _NavList({
    required this.items,
    required this.currentPage,
    required this.onPageChange,
  });

  @override
  Widget build(BuildContext context) {
    return SingleChildScrollView(
      padding: const EdgeInsets.symmetric(vertical: 12, horizontal: 8),
      child: Column(
        children: items
            .map((item) => _NavButton(
                  item: item,
                  isActive: currentPage == item.id,
                  onTap: () => onPageChange(item.id),
                ))
            .toList(),
      ),
    );
  }
}

// ─── Bottom actions (settings) ────────────────────────────────────────────────

class _BottomActions extends StatelessWidget {
  final VoidCallback onSettings;

  const _BottomActions({required this.onSettings});

  @override
  Widget build(BuildContext context) {
    final isDark = Theme.of(context).brightness == Brightness.dark;
    return Container(
      padding: const EdgeInsets.symmetric(vertical: 12, horizontal: 8),
      decoration: BoxDecoration(
        border: Border(
          top: BorderSide(color: isDark ? AppColors.darkBorder : AppColors.lightBorder),
        ),
      ),
      child: _NavButton(
        item: const NavItem(
          id: 'settings',
          label: 'Postavke',
          icon: Icons.settings_rounded,
        ),
        isActive: false,
        onTap: onSettings,
      ),
    );
  }
}

// ─── Individual nav button ────────────────────────────────────────────────────

class _NavButton extends StatelessWidget {
  final NavItem item;
  final bool isActive;
  final VoidCallback onTap;

  const _NavButton({
    required this.item,
    required this.isActive,
    required this.onTap,
  });

  @override
  Widget build(BuildContext context) {
    final scheme = Theme.of(context).colorScheme;
    final isDark = Theme.of(context).brightness == Brightness.dark;
    final activeStart = scheme.primary;
    final activeEnd = isDark ? AppColors.primary : AppColors.primaryDark;
    final onActiveColor = isDark ? AppColors.darkBackground : Colors.white;

    return Padding(
      padding: const EdgeInsets.only(bottom: 2),
      child: Tooltip(
        message: item.label,
        preferBelow: false,
        waitDuration: const Duration(milliseconds: 600),
        child: InkWell(
          onTap: onTap,
          borderRadius: BorderRadius.circular(10),
          splashColor: Colors.transparent,
          highlightColor: Colors.transparent,
          child: Container(
            height: 44,
            decoration: BoxDecoration(
              borderRadius: BorderRadius.circular(10),
              gradient: isActive
                  ? LinearGradient(colors: [activeStart, activeEnd])
                  : null,
              boxShadow: isActive
                  ? [
                      BoxShadow(
                        color: activeStart.withValues(alpha: 0.22),
                        blurRadius: 10,
                        offset: const Offset(0, 4),
                      ),
                    ]
                  : null,
            ),
            // Icon-only — the sidebar is a fixed-width rail with no room for an
            // inline label; the Tooltip above is the only place item.label surfaces.
            child: Center(
              child: Icon(
                item.icon,
                size: 20,
                color: isActive
                    ? onActiveColor
                    : (isDark ? AppColors.darkTextTertiary : AppColors.lightTextTertiary),
              ),
            ),
          ),
        ),
      ),
    );
  }
}
