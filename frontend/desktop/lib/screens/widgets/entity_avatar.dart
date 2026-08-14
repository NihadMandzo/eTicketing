import 'package:flutter/material.dart';

import '../../theme/app_colors.dart';

/// A square, rounded avatar showing [logoUrl] (via `Image.network`) when
/// present and loadable, falling back to a gradient tile with [name]'s
/// initials otherwise — used for organization cards and the organization
/// detail header. Promoted out of organizations_screen.dart's private
/// `_LogoFallback` + inline `ClipRRect(Image.network(...))` block so both
/// places share one implementation.
class EntityAvatar extends StatelessWidget {
  final String name;
  final String? logoUrl;
  final double size;

  const EntityAvatar({
    super.key,
    required this.name,
    this.logoUrl,
    required this.size,
  });

  @override
  Widget build(BuildContext context) {
    return ClipRRect(
      borderRadius: BorderRadius.circular(14),
      child: (logoUrl != null && logoUrl!.isNotEmpty)
          ? Image.network(
              logoUrl!,
              width: size,
              height: size,
              fit: BoxFit.cover,
              errorBuilder: (context, error, stackTrace) => _Fallback(name: name, size: size),
            )
          : _Fallback(name: name, size: size),
    );
  }
}

class _Fallback extends StatelessWidget {
  final String name;
  final double size;

  const _Fallback({required this.name, required this.size});

  @override
  Widget build(BuildContext context) {
    final isDark = Theme.of(context).brightness == Brightness.dark;
    final initials = name.isNotEmpty
        ? name.trim().split(RegExp(r'\s+')).take(2).map((w) => w[0].toUpperCase()).join()
        : '?';
    return Container(
      width: size,
      height: size,
      decoration: BoxDecoration(
        gradient: LinearGradient(
          begin: Alignment.topLeft,
          end: Alignment.bottomRight,
          colors: isDark
              ? [AppColors.secondary, AppColors.primary]
              : [AppColors.primary, AppColors.primaryDark],
        ),
        borderRadius: BorderRadius.circular(14),
      ),
      alignment: Alignment.center,
      child: Text(
        initials,
        style: TextStyle(
          color: isDark ? AppColors.darkBackground : Colors.white,
          fontSize: size * 0.34,
          fontWeight: FontWeight.w700,
          letterSpacing: 1,
        ),
      ),
    );
  }
}
