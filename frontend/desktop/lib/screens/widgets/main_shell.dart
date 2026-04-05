import 'package:flutter/material.dart';

import '../../models/responses/user_profile.dart';
import '../../providers/authorization.dart';
import '../categories_screen.dart';
import '../login_screen.dart';
import '../organizations_screen.dart';
import '../users_screen.dart';
import 'app_header.dart';
import 'app_sidebar.dart';
import 'settings_dialog.dart';

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

  @override
  void initState() {
    super.initState();
    _user = widget.user;
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

  void _logout() {
    Authorization.token = null;
    Navigator.of(context).pushAndRemoveUntil(
      MaterialPageRoute(builder: (_) => const LoginScreen()),
      (_) => false,
    );
  }



  @override
  Widget build(BuildContext context) {
    return Scaffold(
      backgroundColor: const Color(0xFFF8FAFB),
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

  @override
  Widget build(BuildContext context) {
    if (page == 'categories') {
      return const CategoriesScreen();
    }
    if (page == 'organizations') {
      return const OrganizationsScreen();
    }
    if (page == 'users') {
      return UsersScreen(currentUser: user);
    }
    return Padding(
      padding: const EdgeInsets.all(24),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Text(
            _pageTitle(page),
            style: const TextStyle(
              fontSize: 24,
              fontWeight: FontWeight.w700,
              color: Color(0xFF111827),
              letterSpacing: -0.4,
            ),
          ),
          const SizedBox(height: 8),
          Text(
            'Sadržaj za ovu stranicu još nije implementiran.',
            style: const TextStyle(
              fontSize: 14,
              color: Color(0xFF6B7280),
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
      case 'events':
        return 'Događaji';
      case 'categories':
        return 'Kategorije';
      case 'tickets':
        return 'Karte';
      case 'organizations':
        return 'Organizacije';
      case 'users':
        return 'Korisnici';
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
    return Container(
      decoration: BoxDecoration(
        color: Colors.white,
        borderRadius: BorderRadius.circular(16),
        border: Border.all(color: const Color(0xFFE5E7EB)),
        boxShadow: [
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
              color: const Color(0xFF0D7C66).withValues(alpha: 0.1),
              borderRadius: BorderRadius.circular(12),
            ),
            child: const Icon(
              Icons.bar_chart_rounded,
              color: Color(0xFF0D7C66),
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
                  color: const Color(0xFFE5E7EB),
                  borderRadius: BorderRadius.circular(6),
                ),
              ),
              const SizedBox(height: 8),
              Container(
                width: 50,
                height: 20,
                decoration: BoxDecoration(
                  color: const Color(0xFFF3F4F6),
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
