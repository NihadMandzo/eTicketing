import 'package:flutter/material.dart';

import '../../models/responses/user_profile.dart';
import '../../providers/auth_provider.dart';
import '../../theme/app_colors.dart';
import '../categories_screen.dart';
import '../gate_devices_screen.dart';
import '../dashboard_screen.dart';
import '../login_screen.dart';
import '../organizations_screen.dart';
import '../products_screen.dart';
import '../recommendations_screen.dart';
import '../reports_screen.dart';
import '../users_screen.dart';
import 'app_header.dart';
import 'app_sidebar.dart';
import 'settings_dialog.dart';
import '../../core/export_notifications.dart';

/// The post-login application shell.
/// Hosts the sidebar + header and swaps the content area as the user navigates.
class MainShell extends StatefulWidget {
  final UserProfile user;

  const MainShell({super.key, required this.user});

  @override
  State<MainShell> createState() => _MainShellState();
}

class _MainShellState extends State<MainShell> {
  String _currentPage = 'dashboard';
  late UserProfile _user;

  /// Only an organization has exports of its own — platform staff would poll an
  /// endpoint that always answers with an empty list. See [ExportNotifications.roles],
  /// which [AppHeader] reads too so the bell and the polling agree on who this is for.
  bool get _hasExports => ExportNotifications.roles.contains(_user.roleName);

  @override
  void initState() {
    super.initState();
    _user = widget.user;

    // Starts the background watch for finished ticket exports, so a batch the
    // organizer left rendering still reaches them wherever they are in the app.
    if (_hasExports) {
      exportNotifications.start();
    }
  }

  @override
  void dispose() {
    // Covers logout and session expiry alike: both tear this shell down, and
    // one user's exports must never linger in the next user's badge.
    exportNotifications.stop();
    super.dispose();
  }

  void _navigate(String page) {
    setState(() => _currentPage = page);
  }

  Future<void> _openSettings() async {
    final updated = await showSettingsDialog(context, _user);
    if (updated != null && mounted) {
      setState(() => _user = updated);
    }
  }

  Future<void> _logout() async {
    await AuthProvider().logout();
    if (!mounted) return;
    Navigator.of(context).pushAndRemoveUntil(
      MaterialPageRoute(builder: (_) => const LoginScreen()),
      (_) => false,
    );
  }



  @override
  Widget build(BuildContext context) {
    return Scaffold(
      body: Row(
        children: [
          // ── Sidebar ──────────────────────────────────────────────────────
          AppSidebar(
            user: _user,
            currentPage: _currentPage,
            onPageChange: _navigate,
            onSettings: _openSettings,
          ),

          // ── Main content area ─────────────────────────────────────────
          Expanded(
            child: Column(
              children: [
                // Header
                AppHeader(
                  user: _user,
                  onLogout: _logout,
                ),

                // Page content
                Expanded(
                  child: _PageContent(page: _currentPage, user: _user),
                ),
              ],
            ),
          ),
        ],
      ),
    );
  }
}

// ─── Placeholder page content ─────────────────────────────────────────────────

class _PageContent extends StatelessWidget {
  final String page;
  final UserProfile user;

  const _PageContent({required this.page, required this.user});

  /// The four staff roles that have a report matrix at all. `User` (a buyer)
  /// never reaches this shell, but the guard is explicit rather than implied.
  static const _reportRoles = {
    'SuperAdmin',
    'Admin',
    'OrganizationSuperAdmin',
    'OrganizationAdmin',
  };

  @override
  Widget build(BuildContext context) {
    if (page == 'dashboard') {
      return DashboardScreen(user: user);
    }
    if (page == 'categories') {
      return const CategoriesScreen();
    }
    if (page == 'products') {
      return const ProductsScreen();
    }
    if (page == 'gate-devices') {
      return const GateDevicesScreen();
    }
    if (page == 'organizations') {
      return const OrganizationsScreen();
    }
    if (page == 'users') {
      return UsersScreen(currentUser: user);
    }
    if (page == 'reports') {
      // Guarded here as well as in the sidebar, same as `recommendations` below:
      // hiding a nav item is not authorization. Only the four staff roles have a
      // report matrix at all, and the API refuses every request regardless
      // (.claude/rules/21-frontend-desktop.md).
      if (_reportRoles.contains(user.roleName)) {
        return ReportsScreen(user: user);
      }
    }
    if (page == 'recommendations') {
      // Guarded on the role here as well as in the sidebar: hiding a nav item is not
      // authorization, and the screen must not be reachable by any other route either
      // (.claude/rules/21-frontend-desktop.md). The API enforces PlatformStaff regardless.
      if (user.roleName == 'SuperAdmin' || user.roleName == 'Admin') {
        return const RecommendationsScreen();
      }
    }
    final isDark = Theme.of(context).brightness == Brightness.dark;
    return Padding(
      padding: const EdgeInsets.all(24),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Text(
            _pageTitle(page),
            style: TextStyle(
              fontSize: 24,
              fontWeight: FontWeight.w700,
              color: isDark ? AppColors.darkTextPrimary : AppColors.lightTextPrimary,
              letterSpacing: -0.4,
            ),
          ),
          const SizedBox(height: 8),
          Text(
            'Sadržaj za ovu stranicu još nije implementiran.',
            style: TextStyle(
              fontSize: 14,
              color: isDark ? AppColors.darkTextTertiary : AppColors.lightTextTertiary,
            ),
          ),
          const SizedBox(height: 32),
          // Placeholder card grid
          Expanded(
            child: GridView.count(
              crossAxisCount: 3,
              crossAxisSpacing: 16,
              mainAxisSpacing: 16,
              childAspectRatio: 2.2,
              children: List.generate(
                6,
                (i) => const _PlaceholderCard(),
              ),
            ),
          ),
        ],
      ),
    );
  }

  String _pageTitle(String page) {
    switch (page) {
      case 'dashboard':
        return 'Kontrolna tabla';
      case 'products':
        return 'Proizvodi';
      case 'categories':
        return 'Kategorije';
      case 'gate-devices':
        return 'Ulazni uređaji';
      case 'organizations':
        return 'Organizacije';
      case 'users':
        return 'Korisnici';
      case 'recommendations':
        return 'Preporuke';
      case 'reports':
        return 'Izvještaji';
      case 'settings':
        return 'Postavke';
      default:
        return page;
    }
  }
}

class _PlaceholderCard extends StatelessWidget {
  const _PlaceholderCard();

  @override
  Widget build(BuildContext context) {
    final isDark = Theme.of(context).brightness == Brightness.dark;
    final primary = isDark ? AppColors.secondary : AppColors.primary;
    final borderColor = isDark ? AppColors.darkBorder : AppColors.lightBorder;
    return Container(
      decoration: BoxDecoration(
        color: isDark ? AppColors.darkSurface : Colors.white,
        borderRadius: BorderRadius.circular(16),
        border: Border.all(color: borderColor),
        boxShadow: isDark
            ? null
            : [
                BoxShadow(
                  color: Colors.black.withValues(alpha: 0.04),
                  blurRadius: 12,
                  offset: const Offset(0, 4),
                ),
              ],
      ),
      padding: const EdgeInsets.all(20),
      child: Row(
        children: [
          Container(
            width: 48,
            height: 48,
            decoration: BoxDecoration(
              color: primary.withValues(alpha: 0.1),
              borderRadius: BorderRadius.circular(12),
            ),
            child: Icon(
              Icons.bar_chart_rounded,
              color: primary,
              size: 24,
            ),
          ),
          const SizedBox(width: 16),
          Column(
            mainAxisAlignment: MainAxisAlignment.center,
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              Container(
                width: 80,
                height: 12,
                decoration: BoxDecoration(
                  color: borderColor,
                  borderRadius: BorderRadius.circular(6),
                ),
              ),
              const SizedBox(height: 8),
              Container(
                width: 50,
                height: 20,
                decoration: BoxDecoration(
                  color: isDark ? AppColors.darkSurfaceMuted : AppColors.lightSurfaceMuted,
                  borderRadius: BorderRadius.circular(6),
                ),
              ),
            ],
          ),
        ],
      ),
    );
  }
}
