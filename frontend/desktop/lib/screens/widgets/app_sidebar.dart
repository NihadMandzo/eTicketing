import 'package:flutter/material.dart';

import '../../models/responses/user_profile.dart';

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
    id: 'events',
    label: 'Događaji',
    icon: Icons.event_rounded,
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
    id: 'reports',
    label: 'Izvještaji',
    icon: Icons.bar_chart_rounded,
    allowedRoles: ['Admin', 'SuperAdmin', 'OrganizationAdmin', 'OrganizationSuperAdmin'],
  ),
];

// ─── Constants ────────────────────────────────────────────────────────────────

const double _kExpanded = 240.0;
const double _kCollapsed = 72.0;
const Duration _kDur = Duration(milliseconds: 250);
const Color _kPrimary = Color(0xFF0D7C66);
const Color _kPrimaryDark = Color(0xFF0a6b57);

// ─── AppSidebar ───────────────────────────────────────────────────────────────

class AppSidebar extends StatefulWidget {
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

  @override
  State<AppSidebar> createState() => _AppSidebarState();
}

class _AppSidebarState extends State<AppSidebar>
    with SingleTickerProviderStateMixin {
  late final AnimationController _ctrl;
  late final Animation<double> _widthAnim;
  late final Animation<double> _labelFade;

  @override
  void initState() {
    super.initState();
    _ctrl = AnimationController(vsync: this, duration: _kDur);
    _widthAnim = Tween<double>(begin: _kCollapsed, end: _kExpanded)
        .animate(CurvedAnimation(parent: _ctrl, curve: Curves.easeInOut));
    _labelFade = Tween<double>(begin: 0.0, end: 1.0).animate(
        CurvedAnimation(parent: _ctrl, curve: const Interval(0.45, 1.0)));
  }

  @override
  void dispose() {
    _ctrl.dispose();
    super.dispose();
  }

  void _onEnter(_) => _ctrl.forward();
  void _onExit(_) => _ctrl.reverse();

  List<NavItem> get _filtered => kNavItems
      .where((i) =>
          i.allowedRoles.isEmpty ||
          i.allowedRoles.contains(widget.user.roleName))
      .toList();

  @override
  Widget build(BuildContext context) {
    return MouseRegion(
      onEnter: _onEnter,
      onExit: _onExit,
      child: AnimatedBuilder(
        animation: _widthAnim,
        builder: (_, __) => _SidebarShell(
          width: _widthAnim.value,
          labelFade: _labelFade,
          child: Column(
            children: [
              _Logo(labelFade: _labelFade),
              Expanded(
                child: _NavList(
                  items: _filtered,
                  currentPage: widget.currentPage,
                  labelFade: _labelFade,
                  onPageChange: widget.onPageChange,
                ),
              ),
              _BottomActions(
                labelFade: _labelFade,
                onSettings: widget.onSettings,
              ),
            ],
          ),
        ),
      ),
    );
  }
}

// ─── Sidebar shell container ──────────────────────────────────────────────────

class _SidebarShell extends StatelessWidget {
  final double width;
  final Animation<double> labelFade;
  final Widget child;

  const _SidebarShell(
      {required this.width, required this.labelFade, required this.child});

  @override
  Widget build(BuildContext context) {
    return ClipRect(
      child: SizedBox(
        width: width,
        child: DecoratedBox(
          decoration: const BoxDecoration(
            color: Colors.white,
            border:
                Border(right: BorderSide(color: Color(0xFFE5E7EB))),
            boxShadow: [
              BoxShadow(
                color: Color(0x0C000000),
                blurRadius: 16,
                offset: Offset(4, 0),
              ),
            ],
          ),
          child: child,
        ),
      ),
    );
  }
}

// ─── Logo ─────────────────────────────────────────────────────────────────────

class _Logo extends StatelessWidget {
  final Animation<double> labelFade;

  const _Logo({required this.labelFade});

  @override
  Widget build(BuildContext context) {
    return Container(
      height: 64,
      padding: const EdgeInsets.symmetric(horizontal: 16),
      decoration: const BoxDecoration(
        border: Border(bottom: BorderSide(color: Color(0xFFE5E7EB))),
      ),
      child: Row(
        children: [
          // Logo image
          SizedBox(
            width: 40,
            height: 40,
            child: ClipRRect(
              borderRadius: BorderRadius.circular(8),
              child: Image.asset(
                'assets/eTicketing-logo.png',
                fit: BoxFit.contain,
              ),
            ),
          ),
          // App name fades in
          Expanded(
            child: FadeTransition(
              opacity: labelFade,
              child: const Padding(
                padding: EdgeInsets.only(left: 10),
                child: Text(
                  'eKarta Manager',
                  maxLines: 1,
                  overflow: TextOverflow.ellipsis,
                  style: TextStyle(
                    fontSize: 15,
                    fontWeight: FontWeight.w700,
                    color: Color(0xFF111827),
                    letterSpacing: -0.3,
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

// ─── Nav list ─────────────────────────────────────────────────────────────────

class _NavList extends StatelessWidget {
  final List<NavItem> items;
  final String currentPage;
  final Animation<double> labelFade;
  final ValueChanged<String> onPageChange;

  const _NavList({
    required this.items,
    required this.currentPage,
    required this.labelFade,
    required this.onPageChange,
  });

  @override
  Widget build(BuildContext context) {
    return Padding(
      padding: const EdgeInsets.symmetric(vertical: 12, horizontal: 8),
      child: Column(
        children: items
            .map((item) => _NavButton(
                  item: item,
                  isActive: currentPage == item.id,
                  labelFade: labelFade,
                  onTap: () => onPageChange(item.id),
                ))
            .toList(),
      ),
    );
  }
}

// ─── Bottom actions (settings) ────────────────────────────────────────────────

class _BottomActions extends StatelessWidget {
  final Animation<double> labelFade;
  final VoidCallback onSettings;

  const _BottomActions({
    required this.labelFade,
    required this.onSettings,
  });

  @override
  Widget build(BuildContext context) {
    return Container(
      padding: const EdgeInsets.symmetric(vertical: 12, horizontal: 8),
      decoration: const BoxDecoration(
        border: Border(top: BorderSide(color: Color(0xFFE5E7EB))),
      ),
      child: _NavButton(
        item: const NavItem(
          id: 'settings',
          label: 'Postavke',
          icon: Icons.settings_rounded,
        ),
        isActive: false,
        labelFade: labelFade,
        onTap: onSettings,
      ),
    );
  }
}

// ─── Individual nav button ────────────────────────────────────────────────────

class _NavButton extends StatelessWidget {
  final NavItem item;
  final bool isActive;
  final Animation<double> labelFade;
  final VoidCallback onTap;

  const _NavButton({
    required this.item,
    required this.isActive,
    required this.labelFade,
    required this.onTap,
  });

  @override
  Widget build(BuildContext context) {
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
          child: AnimatedContainer(
            duration: const Duration(milliseconds: 200),
            height: 44,
            decoration: BoxDecoration(
              borderRadius: BorderRadius.circular(10),
              gradient: isActive
                  ? const LinearGradient(
                      colors: [_kPrimary, _kPrimaryDark],
                    )
                  : null,
              boxShadow: isActive
                  ? [
                      BoxShadow(
                        color: _kPrimary.withValues(alpha: 0.22),
                        blurRadius: 10,
                        offset: const Offset(0, 4),
                      ),
                    ]
                  : null,
            ),
            child: Row(
              children: [
                // Icon — fixed width so it never overflows
                SizedBox(
                  width: 56,
                  child: Center(
                    child: Icon(
                      item.icon,
                      size: 20,
                      color: isActive
                          ? Colors.white
                          : const Color(0xFF6B7280),
                    ),
                  ),
                ),
                // Label — takes remaining space, fades in
                Expanded(
                  child: FadeTransition(
                    opacity: labelFade,
                    child: Text(
                      item.label,
                      maxLines: 1,
                      overflow: TextOverflow.ellipsis,
                      style: TextStyle(
                        fontSize: 14,
                        fontWeight: isActive
                            ? FontWeight.w600
                            : FontWeight.w500,
                        color: isActive
                            ? Colors.white
                            : const Color(0xFF374151),
                      ),
                    ),
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
