import 'package:flutter/material.dart';

import '../core/session.dart';
import '../services/auth_service.dart';
import '../theme/app_colors.dart';
import '../widgets/responsive_page.dart';
import 'login_screen.dart';
import 'register_screen.dart';
import 'settings_screen.dart';

/// The guest-accessible landing screen — always reachable without signing
/// in. Login/registration are optional entry points from here, not a gate.
class HomeScreen extends StatelessWidget {
  const HomeScreen({super.key});

  @override
  Widget build(BuildContext context) {
    final scheme = Theme.of(context).colorScheme;
    final isDark = Theme.of(context).brightness == Brightness.dark;

    return Scaffold(
      appBar: AppBar(
        title: const Text('eKarta'),
        actions: [
          IconButton(
            icon: const Icon(Icons.settings_outlined),
            tooltip: 'Postavke',
            onPressed: () => Navigator.of(context).push(
              MaterialPageRoute(builder: (_) => const SettingsScreen()),
            ),
          ),
          ValueListenableBuilder(
            valueListenable: Session.currentUser,
            builder: (context, user, _) {
              if (user == null) return const SizedBox.shrink();
              return IconButton(
                icon: const Icon(Icons.logout_rounded),
                tooltip: 'Odjavi se',
                onPressed: () => _confirmLogout(context),
              );
            },
          ),
        ],
      ),
      body: SafeArea(
        child: SingleChildScrollView(
          child: ResponsivePage(
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.stretch,
              children: [
                const SizedBox(height: 24),
                Icon(Icons.confirmation_number_rounded, size: 72, color: scheme.primary),
                const SizedBox(height: 16),
                Text(
                  'Dobrodošli u eKartu',
                  textAlign: TextAlign.center,
                  style: TextStyle(
                    fontSize: 26,
                    fontWeight: FontWeight.w700,
                    color: isDark ? AppColors.darkTextPrimary : AppColors.lightTextPrimary,
                  ),
                ),
                const SizedBox(height: 8),
                Text(
                  'Pregledajte događaje kao gost, ili se prijavite da kupujete karte.',
                  textAlign: TextAlign.center,
                  style: TextStyle(
                    fontSize: 15,
                    color: isDark ? AppColors.darkTextTertiary : AppColors.lightTextTertiary,
                  ),
                ),
                const SizedBox(height: 32),
                ValueListenableBuilder(
                  valueListenable: Session.currentUser,
                  builder: (context, user, _) {
                    if (user != null) {
                      return _SignedInCard(name: user.fullName, email: user.email);
                    }
                    return Column(
                      children: [
                        FilledButton(
                          style: FilledButton.styleFrom(
                            backgroundColor: scheme.primary,
                            foregroundColor: scheme.onPrimary,
                            padding: const EdgeInsets.symmetric(vertical: 14),
                          ),
                          onPressed: () => Navigator.of(context).push(
                            MaterialPageRoute(builder: (_) => const LoginScreen()),
                          ),
                          child: const Text('Prijava'),
                        ),
                        const SizedBox(height: 12),
                        OutlinedButton(
                          style: OutlinedButton.styleFrom(
                            foregroundColor: scheme.primary,
                            side: BorderSide(color: scheme.primary),
                            padding: const EdgeInsets.symmetric(vertical: 14),
                          ),
                          onPressed: () => Navigator.of(context).push(
                            MaterialPageRoute(builder: (_) => const RegisterScreen()),
                          ),
                          child: const Text('Registracija'),
                        ),
                      ],
                    );
                  },
                ),
                const SizedBox(height: 32),
                const _GuestNotice(),
              ],
            ),
          ),
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
  }
}

class _SignedInCard extends StatelessWidget {
  final String name;
  final String email;

  const _SignedInCard({required this.name, required this.email});

  @override
  Widget build(BuildContext context) {
    final isDark = Theme.of(context).brightness == Brightness.dark;
    final primary = Theme.of(context).colorScheme.primary;
    final tertiaryText = isDark ? AppColors.darkTextTertiary : AppColors.lightTextTertiary;
    return Container(
      padding: const EdgeInsets.all(16),
      decoration: BoxDecoration(
        color: isDark ? AppColors.darkSurfaceTint : AppColors.lightSurfaceTint,
        borderRadius: BorderRadius.circular(12),
        border: Border.all(color: primary.withValues(alpha: 0.2)),
      ),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Text('Prijavljeni ste kao', style: TextStyle(fontSize: 12, color: tertiaryText)),
          const SizedBox(height: 4),
          Text(name, style: const TextStyle(fontSize: 16, fontWeight: FontWeight.w700)),
          Text(email, style: TextStyle(fontSize: 13, color: tertiaryText)),
        ],
      ),
    );
  }
}

class _GuestNotice extends StatelessWidget {
  const _GuestNotice();

  @override
  Widget build(BuildContext context) {
    final isDark = Theme.of(context).brightness == Brightness.dark;
    final color = isDark ? AppColors.darkTextTertiary : AppColors.lightTextDisabled;
    return Row(
      mainAxisAlignment: MainAxisAlignment.center,
      children: [
        Icon(Icons.info_outline_rounded, size: 16, color: color),
        const SizedBox(width: 6),
        Flexible(
          child: Text(
            'Pregledanje je moguće i bez naloga.',
            style: TextStyle(fontSize: 12, color: color),
          ),
        ),
      ],
    );
  }
}
