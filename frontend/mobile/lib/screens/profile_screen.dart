import 'package:flutter/material.dart';

import '../core/session.dart';
import '../models/responses/user_response.dart';
import '../services/auth_service.dart';
import '../theme/app_colors.dart';
import '../theme/theme_controller.dart';
import '../widgets/initials_avatar.dart';
import '../widgets/responsive_page.dart';
import 'change_password_screen.dart';
import 'contact_screen.dart';
import 'edit_profile_screen.dart';
import 'help_screen.dart';
import 'login_screen.dart';
import 'my_tickets_screen.dart';
import 'privacy_screen.dart';
import 'terms_screen.dart';

/// The "Profil" tab body inside [MainShell] — account summary + personal
/// info/change-password/order-history entry points, the light/dark toggle,
/// and logout. No `Scaffold`/`AppBar` of its own: [MainShell] supplies both
/// so the bottom nav bar and app bar stay consistent across tabs.
class ProfileScreen extends StatelessWidget {
  const ProfileScreen({super.key});

  @override
  Widget build(BuildContext context) {
    return ValueListenableBuilder<UserResponse?>(
      valueListenable: Session.currentUser,
      builder: (context, user, _) {
        // MainShell only exists once signed in, but fall back gracefully
        // rather than crash if the session drops while this tab is visible
        // (e.g. token revoked elsewhere).
        if (user == null) {
          return const Center(child: Text('Niste prijavljeni.'));
        }
        return SingleChildScrollView(
          child: ResponsivePage(
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.stretch,
              children: [
                const SizedBox(height: 16),
                Center(
                  child: Column(
                    children: [
                      InitialsAvatar(name: user.fullName, radius: 36),
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
                      const Divider(height: 1),
                      _ProfileRow(
                        icon: Icons.receipt_long_outlined,
                        label: 'Historija narudžbi',
                        onTap: () => Navigator.of(context).push(
                          MaterialPageRoute(
                            builder: (_) => Scaffold(
                              appBar: AppBar(title: const Text('Historija narudžbi')),
                              body: const SafeArea(child: MyTicketsScreen()),
                            ),
                          ),
                        ),
                      ),
                    ],
                  ),
                ),
                const SizedBox(height: 16),
                // Legal/static pages — mirrors web's footer pages
                // (Pomoć/Uslovi/Privatnost/Kontakt), which had no mobile
                // equivalent before.
                Card(
                  clipBehavior: Clip.antiAlias,
                  child: Column(
                    children: [
                      _ProfileRow(
                        icon: Icons.help_outline_rounded,
                        label: 'Pomoć',
                        onTap: () => Navigator.of(context).push(MaterialPageRoute(builder: (_) => const HelpScreen())),
                      ),
                      const Divider(height: 1),
                      _ProfileRow(
                        icon: Icons.description_outlined,
                        label: 'Uslovi korištenja',
                        onTap: () => Navigator.of(context).push(MaterialPageRoute(builder: (_) => const TermsScreen())),
                      ),
                      const Divider(height: 1),
                      _ProfileRow(
                        icon: Icons.privacy_tip_outlined,
                        label: 'Politika privatnosti',
                        onTap: () => Navigator.of(context).push(MaterialPageRoute(builder: (_) => const PrivacyScreen())),
                      ),
                      const Divider(height: 1),
                      _ProfileRow(
                        icon: Icons.mail_outline_rounded,
                        label: 'Kontakt',
                        onTap: () => Navigator.of(context).push(MaterialPageRoute(builder: (_) => const ContactScreen())),
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
    if (!context.mounted) return;
    // The app is login-gated (see LoginScreen/MainShell) — signing out
    // always lands back on the login screen, replacing the whole stack
    // rather than popping (this tab can be several pushes deep).
    Navigator.of(context).pushAndRemoveUntil(
      MaterialPageRoute(builder: (_) => const LoginScreen()),
      (route) => false,
    );
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
