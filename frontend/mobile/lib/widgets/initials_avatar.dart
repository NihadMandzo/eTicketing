import 'package:flutter/material.dart';

/// Circular initials avatar — matches the design mockup's avatar treatment
/// (solid primary-color circle, white bold initials). Shared between the
/// Profil tab's header and the Događaji tab's brand row.
class InitialsAvatar extends StatelessWidget {
  final String name;
  final double radius;
  final VoidCallback? onTap;

  const InitialsAvatar({super.key, required this.name, this.radius = 18, this.onTap});

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
    final avatar = CircleAvatar(
      radius: radius,
      backgroundColor: primary,
      child: Text(
        _initials,
        style: TextStyle(color: Colors.white, fontWeight: FontWeight.w700, fontSize: radius * 0.6),
      ),
    );
    if (onTap == null) return avatar;
    return InkWell(borderRadius: BorderRadius.circular(radius), onTap: onTap, child: avatar);
  }
}
