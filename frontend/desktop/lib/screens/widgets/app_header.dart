import 'package:flutter/material.dart';

import '../../models/responses/user_profile.dart';

const Color _kPrimary = Color(0xFF0D7C66);
const Color _kPrimaryDark = Color(0xFF0a6b57);

class AppHeader extends StatefulWidget {
  final UserProfile user;
  final VoidCallback onLogout;

  const AppHeader({
    super.key,
    required this.user,
    required this.onLogout,
  });

  @override
  State<AppHeader> createState() => _AppHeaderState();
}

class _AppHeaderState extends State<AppHeader> {
  bool _menuOpen = false;
  final _layerLink = LayerLink();
  OverlayEntry? _overlay;

  String _roleDisplay(String role) {
    switch (role) {
      case 'SuperAdmin':
        return 'Super Administrator';
      case 'Admin':
        return 'Administrator';
      case 'OrganizationSuperAdmin':
        return 'Org. Super Administrator';
      case 'OrganizationAdmin':
        return 'Org. Administrator';
      default:
        return role;
    }
  }

  // ── Overlay lifecycle ─────────────────────────────────────────────────────

  void _openMenu() {
    if (_menuOpen) return;
    setState(() => _menuOpen = true);
    _overlay = OverlayEntry(builder: (_) => _buildOverlay());
    Overlay.of(context).insert(_overlay!);
  }

  void _closeMenu() {
    if (!_menuOpen) return;
    setState(() => _menuOpen = false);
    _overlay?.remove();
    _overlay = null;
  }

  void _toggle() => _menuOpen ? _closeMenu() : _openMenu();

  @override
  void dispose() {
    _overlay?.remove();
    super.dispose();
  }

  // ── Overlay widget ────────────────────────────────────────────────────────

  Widget _buildOverlay() {
    return Stack(
      children: [
        Positioned.fill(
          child: GestureDetector(
            behavior: HitTestBehavior.translucent,
            onTap: _closeMenu,
          ),
        ),
        CompositedTransformFollower(
          link: _layerLink,
          showWhenUnlinked: false,
          targetAnchor: Alignment.bottomRight,
          followerAnchor: Alignment.topRight,
          offset: const Offset(0, 8),
          child: Material(
            color: Colors.transparent,
            child: _DropdownCard(
              user: widget.user,
              roleDisplay: _roleDisplay(widget.user.roleName),
              onLogout: () {
                _closeMenu();
                widget.onLogout();
              },
            ),
          ),
        ),
      ],
    );
  }

  // ── Avatar ────────────────────────────────────────────────────────────────

  Widget _avatar({double size = 36}) {
    final f = widget.user.firstName.isNotEmpty
        ? widget.user.firstName[0].toUpperCase()
        : '';
    final l = widget.user.lastName.isNotEmpty
        ? widget.user.lastName[0].toUpperCase()
        : '';
    final initials = f.isEmpty && l.isEmpty ? '?' : '$f$l';
    return Container(
      width: size,
      height: size,
      decoration: const BoxDecoration(
        shape: BoxShape.circle,
        gradient: LinearGradient(
          begin: Alignment.topLeft,
          end: Alignment.bottomRight,
          colors: [_kPrimary, _kPrimaryDark],
        ),
      ),
      alignment: Alignment.center,
      child: Text(
        initials,
        style: TextStyle(
          color: Colors.white,
          fontWeight: FontWeight.w700,
          fontSize: size * 0.36,
        ),
      ),
    );
  }

  @override
  Widget build(BuildContext context) {
    return Container(
      height: 64,
      decoration: const BoxDecoration(
        color: Colors.white,
        border: Border(bottom: BorderSide(color: Color(0xFFE5E7EB))),
        boxShadow: [
          BoxShadow(
            color: Color(0x08000000),
            blurRadius: 8,
            offset: Offset(0, 2),
          ),
        ],
      ),
      padding: const EdgeInsets.symmetric(horizontal: 24),
      child: Row(
        children: [
          // ── Search spacer ──────────────────────────────────────────────────────
          const Spacer(),

          const SizedBox(width: 16),

          // ── Notification bell ────────────────────────────────────────────
          const _BellButton(),

          const SizedBox(width: 8),

          // ── User button ──────────────────────────────────────────────────
          CompositedTransformTarget(
            link: _layerLink,
            child: _UserButton(
              avatar: _avatar(),
              name: widget.user.fullName,
              role: _roleDisplay(widget.user.roleName),
              isOpen: _menuOpen,
              onTap: _toggle,
            ),
          ),
        ],
      ),
    );
  }
}

// ─── User trigger button ──────────────────────────────────────────────────────

class _UserButton extends StatefulWidget {
  final Widget avatar;
  final String name;
  final String role;
  final bool isOpen;
  final VoidCallback onTap;

  const _UserButton({
    required this.avatar,
    required this.name,
    required this.role,
    required this.isOpen,
    required this.onTap,
  });

  @override
  State<_UserButton> createState() => _UserButtonState();
}

class _UserButtonState extends State<_UserButton> {
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
          duration: const Duration(milliseconds: 150),
          padding:
              const EdgeInsets.symmetric(horizontal: 10, vertical: 6),
          decoration: BoxDecoration(
            color: _hovered || widget.isOpen
                ? const Color(0xFFF3F4F6)
                : Colors.transparent,
            borderRadius: BorderRadius.circular(10),
          ),
          child: Row(
            mainAxisSize: MainAxisSize.min,
            children: [
              widget.avatar,
              const SizedBox(width: 10),
              Column(
                mainAxisAlignment: MainAxisAlignment.center,
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  Text(
                    widget.name,
                    style: const TextStyle(
                      fontSize: 13,
                      fontWeight: FontWeight.w600,
                      color: Color(0xFF111827),
                    ),
                  ),
                  Text(
                    widget.role,
                    style: const TextStyle(
                      fontSize: 11,
                      color: Color(0xFF6B7280),
                    ),
                  ),
                ],
              ),
              const SizedBox(width: 6),
              AnimatedRotation(
                turns: widget.isOpen ? 0.5 : 0,
                duration: const Duration(milliseconds: 200),
                child: const Icon(
                  Icons.keyboard_arrow_down_rounded,
                  size: 18,
                  color: Color(0xFF6B7280),
                ),
              ),
            ],
          ),
        ),
      ),
    );
  }
}

// ─── Dropdown card (matches the React design) ─────────────────────────────────

class _DropdownCard extends StatelessWidget {
  final UserProfile user;
  final String roleDisplay;
  final VoidCallback onLogout;

  const _DropdownCard({
    required this.user,
    required this.roleDisplay,
    required this.onLogout,
  });

  @override
  Widget build(BuildContext context) {
    return Container(
      width: 264,
      decoration: BoxDecoration(
        color: Colors.white,
        borderRadius: BorderRadius.circular(14),
        border: Border.all(color: const Color(0xFFE5E7EB)),
        boxShadow: [
          BoxShadow(
            color: Colors.black.withValues(alpha: 0.12),
            blurRadius: 28,
            offset: const Offset(0, 8),
          ),
        ],
      ),
      child: Column(
        mainAxisSize: MainAxisSize.min,
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          // User info section
          Padding(
            padding: const EdgeInsets.fromLTRB(16, 16, 16, 14),
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Text(
                  user.fullName,
                  style: const TextStyle(
                    fontSize: 15,
                    fontWeight: FontWeight.w600,
                    color: Color(0xFF111827),
                  ),
                ),
                const SizedBox(height: 3),
                Text(
                  user.email,
                  style: const TextStyle(
                    fontSize: 13,
                    color: Color(0xFF6B7280),
                  ),
                ),
                if (user.organizationName != null) ...[
                  const SizedBox(height: 4),
                  Text(
                    user.organizationName!,
                    style: const TextStyle(
                      fontSize: 13,
                      fontWeight: FontWeight.w500,
                      color: _kPrimary,
                    ),
                  ),
                ],
              ],
            ),
          ),

          const Divider(height: 1, thickness: 1, color: Color(0xFFE5E7EB)),

          // Logout
          _DropdownAction(
            icon: Icons.logout_rounded,
            label: 'Odjavi se',
            color: const Color(0xFFEF4444),
            hoverBg: const Color(0xFFFEF2F2),
            onTap: onLogout,
          ),

          const SizedBox(height: 4),
        ],
      ),
    );
  }
}

// ─── Dropdown action row ──────────────────────────────────────────────────────

class _DropdownAction extends StatefulWidget {
  final IconData icon;
  final String label;
  final Color color;
  final Color hoverBg;
  final VoidCallback onTap;

  const _DropdownAction({
    required this.icon,
    required this.label,
    required this.color,
    required this.hoverBg,
    required this.onTap,
  });

  @override
  State<_DropdownAction> createState() => _DropdownActionState();
}

class _DropdownActionState extends State<_DropdownAction> {
  bool _hovered = false;

  @override
  Widget build(BuildContext context) {
    return MouseRegion(
      cursor: SystemMouseCursors.click,
      onEnter: (_) => setState(() => _hovered = true),
      onExit: (_) => setState(() => _hovered = false),
      child: GestureDetector(
        onTap: widget.onTap,
        child: Padding(
          padding: const EdgeInsets.fromLTRB(8, 4, 8, 0),
          child: AnimatedContainer(
            duration: const Duration(milliseconds: 120),
            decoration: BoxDecoration(
              color: _hovered ? widget.hoverBg : Colors.transparent,
              borderRadius: BorderRadius.circular(8),
            ),
            padding:
                const EdgeInsets.symmetric(horizontal: 12, vertical: 11),
            child: Row(
              children: [
                Icon(widget.icon, size: 18, color: widget.color),
                const SizedBox(width: 10),
                Text(
                  widget.label,
                  style: TextStyle(
                    fontSize: 14,
                    fontWeight: FontWeight.w500,
                    color: widget.color,
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

// ─── Notification bell ────────────────────────────────────────────────────────

class _BellButton extends StatefulWidget {
  const _BellButton();

  @override
  State<_BellButton> createState() => _BellButtonState();
}

class _BellButtonState extends State<_BellButton> {
  bool _hovered = false;

  @override
  Widget build(BuildContext context) {
    return MouseRegion(
      cursor: SystemMouseCursors.click,
      onEnter: (_) => setState(() => _hovered = true),
      onExit: (_) => setState(() => _hovered = false),
      child: GestureDetector(
        onTap: () {},
        child: AnimatedContainer(
          duration: const Duration(milliseconds: 150),
          width: 40,
          height: 40,
          decoration: BoxDecoration(
            color:
                _hovered ? const Color(0xFFF3F4F6) : Colors.transparent,
            borderRadius: BorderRadius.circular(10),
          ),
          child: Stack(
            children: [
              Center(
                child: Icon(
                  Icons.notifications_outlined,
                  size: 22,
                  color: _hovered
                      ? const Color(0xFF111827)
                      : const Color(0xFF6B7280),
                ),
              ),
              Positioned(
                top: 8,
                right: 8,
                child: Container(
                  width: 8,
                  height: 8,
                  decoration: const BoxDecoration(
                    color: Color(0xFFFF6F3C),
                    shape: BoxShape.circle,
                  ),
                ),
              ),
            ],
          ),
        ),
      ),
    );
  }
}
