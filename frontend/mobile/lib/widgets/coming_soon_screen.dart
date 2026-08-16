import 'package:flutter/material.dart';

import '../theme/app_colors.dart';

/// Honest placeholder for a section the design mockup (see the "Design
/// reference" note in `.claude/rules/22-frontend-mobile.md`) shows fully
/// built out, but whose backend isn't wired into mobile yet (Ticketing for
/// order history). Shows the section's own icon/title rather than
/// fabricated data — swap this out for the real screen once that service
/// integration lands. Pushed as its own route (e.g. from `ProfileScreen`'s
/// "Historija narudžbi" row), so it keeps its own `Scaffold`/`AppBar` for
/// back navigation — unlike `MainShell`'s root tabs, which have no system
/// app bar at all.
class ComingSoonScreen extends StatelessWidget {
  final String title;
  final IconData icon;
  final String message;

  const ComingSoonScreen({super.key, required this.title, required this.icon, required this.message});

  @override
  Widget build(BuildContext context) {
    final isDark = Theme.of(context).brightness == Brightness.dark;
    final tertiaryText = isDark ? AppColors.darkTextTertiary : AppColors.lightTextTertiary;
    final scheme = Theme.of(context).colorScheme;

    return Scaffold(
      appBar: AppBar(title: Text(title)),
      body: SafeArea(
        child: Center(
          child: Padding(
            padding: const EdgeInsets.all(32),
            child: Column(
              mainAxisSize: MainAxisSize.min,
              children: [
                Icon(icon, size: 56, color: scheme.primary),
                const SizedBox(height: 16),
                Text(
                  title,
                  style: TextStyle(
                    fontSize: 18,
                    fontWeight: FontWeight.w700,
                    color: isDark ? AppColors.darkTextPrimary : AppColors.lightTextPrimary,
                  ),
                ),
                const SizedBox(height: 8),
                Text(message, textAlign: TextAlign.center, style: TextStyle(fontSize: 14, color: tertiaryText)),
              ],
            ),
          ),
        ),
      ),
    );
  }
}
