import 'package:flutter/material.dart';

import '../core/session.dart';
import '../services/auth_service.dart';
import 'login_screen.dart';
import 'register_screen.dart';

/// The guest-accessible landing screen — always reachable without signing
/// in. Login/registration are optional entry points from here, not a gate.
class HomeScreen extends StatelessWidget {
  const HomeScreen({super.key});

  static const _primary = Color(0xFF0D7C66);

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      appBar: AppBar(
        title: const Text('eKarta'),
        backgroundColor: _primary,
        foregroundColor: Colors.white,
        actions: [
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
        child: Padding(
          padding: const EdgeInsets.all(24),
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.stretch,
            children: [
              const SizedBox(height: 24),
              const Icon(Icons.confirmation_number_rounded, size: 72, color: _primary),
              const SizedBox(height: 16),
              const Text(
                'Dobrodošli u eKartu',
                textAlign: TextAlign.center,
                style: TextStyle(fontSize: 26, fontWeight: FontWeight.w700, color: Color(0xFF111827)),
              ),
              const SizedBox(height: 8),
              const Text(
                'Pregledajte događaje kao gost, ili se prijavite da kupujete karte.',
                textAlign: TextAlign.center,
                style: TextStyle(fontSize: 15, color: Color(0xFF6B7280)),
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
                          backgroundColor: _primary,
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
                          foregroundColor: _primary,
                          side: const BorderSide(color: _primary),
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
    return Container(
      padding: const EdgeInsets.all(16),
      decoration: BoxDecoration(
        color: const Color(0xFFF0FDF9),
        borderRadius: BorderRadius.circular(12),
        border: Border.all(color: const Color(0xFF0D7C66).withValues(alpha: 0.2)),
      ),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          const Text('Prijavljeni ste kao', style: TextStyle(fontSize: 12, color: Color(0xFF6B7280))),
          const SizedBox(height: 4),
          Text(name, style: const TextStyle(fontSize: 16, fontWeight: FontWeight.w700)),
          Text(email, style: const TextStyle(fontSize: 13, color: Color(0xFF6B7280))),
        ],
      ),
    );
  }
}

class _GuestNotice extends StatelessWidget {
  const _GuestNotice();

  @override
  Widget build(BuildContext context) {
    return Row(
      mainAxisAlignment: MainAxisAlignment.center,
      children: const [
        Icon(Icons.info_outline_rounded, size: 16, color: Color(0xFF9CA3AF)),
        SizedBox(width: 6),
        Flexible(
          child: Text(
            'Pregledanje je moguće i bez naloga.',
            style: TextStyle(fontSize: 12, color: Color(0xFF9CA3AF)),
          ),
        ),
      ],
    );
  }
}
