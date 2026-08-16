import 'package:flutter/material.dart';

import '../core/session.dart';
import '../models/responses/user_response.dart';
import '../services/auth_service.dart';
import '../theme/app_colors.dart';
import '../theme/theme_controller.dart';
import '../widgets/responsive_page.dart';
import 'change_password_screen.dart';
import 'edit_profile_screen.dart';

/// Reachable from the avatar button on [HomeScreen]'s app bar, only when
/// signed in. Holds the account summary + personal-info/change-password
/// entry points + the light/dark toggle + logout — mirrors the "Profil"
/// screen in the design mockup referenced in the mobile frontend rules.
class ProfileScreen extends StatelessWidget {
  const ProfileScreen({super.key});

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      appBar: AppBar(title: const Text('Profil')),
      body: SafeArea(
        child: ValueListenableBuilder<UserResponse?>(
          valueListenable: Session.currentUser,
          builder: (context, user, _) {
            // Guarded by the caller (only navigated to when signed in), but
            // fall back gracefully rather than crash if the session drops
            // while this screen is open (e.g. token revoked elsewhere).
            if (user == null) {
              return const Center(child: Text('Niste prijavljeni.'));
            }
            return SingleChildScrollView(
              child: ResponsivePage(
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.stretch,
                  children: [
                    const SizedBox(height: 8),
                    Center(
                      child: Column(
                        children: [
                          _InitialsAvatar(name: user.fullName, radius: 36),
                          const SizedBox(height: 12),
                          Text(user.fullName, style: const TextStyle(fontSize: 18, fontWeight: FontWeight.w700)),
                          const SizedBox(height: 2),
                          Text(
                            user.email,
                            style: TextStyle(
                              fontSize: 13,
                              color: Theme.of(context).brightness == Brightness.dark
                                  ? AppColors.darkTextTertiary
                                  : AppColors.lightTextTertiary,
                            ),
                          ),
                        ],
                      ),
                    ),
                    const SizedBox(height: 28),
                    Card(
                      clipBehavior: Clip.antiAlias,
                      child: Column(
                        children: [
                          _ProfileRow(
                            icon: Icons.person_outline_rounded,
                            label: 'Lični podaci',
                            onTap: () => Navigator.of(context).push(
                              MaterialPageRoute(builder: (_) => const EditProfileScreen()),
                            ),
                          ),
                          const Divider(height: 1),
                          _ProfileRow(
                            icon: Icons.lock_outline_rounded,
                            label: 'Promijeni lozinku',
                            onTap: () => Navigator.of(context).push(
                              MaterialPageRoute(builder: (_) => const ChangePasswordScreen()),
                            ),
                          ),
                        ],
                      ),
                    ),
                    const SizedBox(height: 16),
                    Card(
                      clipBehavior: Clip.antiAlias,
                      child: _DarkModeRow(),
                    ),
                    const SizedBox(height: 24),
                    OutlinedButton.icon(
                      onPressed: () => _confirmLogout(context),
                      icon: const Icon(Icons.logout_rounded, color: AppColors.errorDark),
                      label: const Text('Odjava', style: TextStyle(color: AppColors.errorDark, fontWeight: FontWeight.w700)),
                      style: OutlinedButton.styleFrom(
                        side: const BorderSide(color: AppColors.errorDark, width: 2),
                        padding: const EdgeInsets.symmetric(vertical: 14),
                        shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(12)),
                      ),
                    ),
                  ],
                ),
              ),
            );
          },
        ),
      ),
    );
  }

  Future<void> _confirmLogout(BuildContext context) async {
    final confirmed = await showDialog<bool>(
      context: context,
      builder: (ctx) => AlertDialog(
        title: const Text('Odjava'),
        content: const Text('Da li se želite odjaviti?'),
        actions: [
          TextButton(onPressed: () => Navigator.of(ctx).pop(false), child: const Text('Odustani')),
          TextButton(onPressed: () => Navigator.of(ctx).pop(true), child: const Text('Odjavi se')),
        ],
      ),
    );
    if (confirmed != true) return;

    await AuthService().logout();
    if (context.mounted) Navigator.of(context).popUntil((route) => route.isFirst);
  }
}

class _ProfileRow extends StatelessWidget {
  final IconData icon;
  final String label;
  final VoidCallback onTap;

  const _ProfileRow({required this.icon, required this.label, required this.onTap});

  @override
  Widget build(BuildContext context) {
    final primary = Theme.of(context).colorScheme.primary;
    final isDark = Theme.of(context).brightness == Brightness.dark;
    return InkWell(
      onTap: onTap,
      child: Padding(
        padding: const EdgeInsets.symmetric(horizontal: 16, vertical: 16),
        child: Row(
          children: [
            Icon(icon, size: 18, color: primary),
            const SizedBox(width: 12),
            Expanded(child: Text(label, style: const TextStyle(fontSize: 14, fontWeight: FontWeight.w600))),
            Icon(Icons.chevron_right_rounded, size: 20, color: isDark ? AppColors.darkTextTertiary : AppColors.lightTextTertiary),
          ],
        ),
      ),
    );
  }
}

class _DarkModeRow extends StatelessWidget {
  @override
  Widget build(BuildContext context) {
    final isDark = ThemeController.isDark(context);
    final primary = Theme.of(context).colorScheme.primary;
    return Padding(
      padding: const EdgeInsets.symmetric(horizontal: 16, vertical: 8),
      child: Row(
        children: [
          Icon(isDark ? Icons.dark_mode_outlined : Icons.light_mode_outlined, size: 18, color: primary),
          const SizedBox(width: 12),
          const Expanded(child: Text('Tamni način rada', style: TextStyle(fontSize: 14, fontWeight: FontWeight.w600))),
          Switch(
            value: isDark,
            onChanged: (v) => ThemeController.setMode(v ? ThemeMode.dark : ThemeMode.light),
          ),
        ],
      ),
    );
  }
}

/// Circular initials avatar — used both on this screen and (smaller) in
/// [HomeScreen]'s app bar, matching the design mockup's avatar treatment.
class _InitialsAvatar extends StatelessWidget {
  final String name;
  final double radius;

  const _InitialsAvatar({required this.name, this.radius = 18});

  String get _initials {
    final parts = name.trim().split(RegExp(r'\s+')).where((p) => p.isNotEmpty).toList();
    if (parts.isEmpty) return '?';
    final first = parts.first[0];
    final last = parts.length > 1 ? parts.last[0] : '';
    return (first + last).toUpperCase();
  }

  @override
  Widget build(BuildContext context) {
    final primary = Theme.of(context).colorScheme.primary;
    return CircleAvatar(
      radius: radius,
      backgroundColor: primary,
      child: Text(
        _initials,
        style: TextStyle(color: Colors.white, fontWeight: FontWeight.w700, fontSize: radius * 0.6),
      ),
    );
  }
}
