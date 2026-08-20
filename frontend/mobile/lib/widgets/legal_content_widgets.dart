import 'package:flutter/material.dart';

import '../theme/app_colors.dart';

/// Shared building blocks for the static/legal content screens
/// (help/terms/privacy/contact) — content ported verbatim from
/// `frontend/web`'s equivalent pages, for feature parity between the two
/// apps (web had these, mobile didn't).

/// A numbered/titled section with one or more paragraphs and/or a bullet
/// list — mirrors web's `.terms-section`/`.privacy-section` blocks.
class LegalSection extends StatelessWidget {
  final String title;
  final List<String> paragraphs;
  final List<String> bullets;

  const LegalSection({super.key, required this.title, this.paragraphs = const [], this.bullets = const []});

  @override
  Widget build(BuildContext context) {
    final isDark = Theme.of(context).brightness == Brightness.dark;
    final bodyColor = isDark ? AppColors.darkTextSecondary : AppColors.lightTextSecondary;
    return Padding(
      padding: const EdgeInsets.only(bottom: 20),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Text(title, style: const TextStyle(fontSize: 16, fontWeight: FontWeight.w700)),
          const SizedBox(height: 8),
          for (final p in paragraphs)
            Padding(
              padding: const EdgeInsets.only(bottom: 8),
              child: Text(p, style: TextStyle(fontSize: 14, height: 1.5, color: bodyColor)),
            ),
          for (final b in bullets)
            Padding(
              padding: const EdgeInsets.only(bottom: 6, left: 4),
              child: Row(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  Text('•  ', style: TextStyle(fontSize: 14, color: bodyColor)),
                  Expanded(child: Text(b, style: TextStyle(fontSize: 14, height: 1.4, color: bodyColor))),
                ],
              ),
            ),
        ],
      ),
    );
  }
}

/// A single FAQ question/answer pair — mirrors web's `.faq-item`.
class FaqItem extends StatelessWidget {
  final String question;
  final String answer;

  const FaqItem({super.key, required this.question, required this.answer});

  @override
  Widget build(BuildContext context) {
    final isDark = Theme.of(context).brightness == Brightness.dark;
    final bodyColor = isDark ? AppColors.darkTextSecondary : AppColors.lightTextSecondary;
    return Padding(
      padding: const EdgeInsets.only(bottom: 14),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Text(question, style: const TextStyle(fontSize: 14, fontWeight: FontWeight.w700)),
          const SizedBox(height: 4),
          Text(answer, style: TextStyle(fontSize: 13, height: 1.5, color: bodyColor)),
        ],
      ),
    );
  }
}

/// A group of [FaqItem]s under one heading — mirrors web's `.help-section`.
class FaqGroup extends StatelessWidget {
  final String title;
  final List<FaqItem> items;

  const FaqGroup({super.key, required this.title, required this.items});

  @override
  Widget build(BuildContext context) {
    return Padding(
      padding: const EdgeInsets.only(bottom: 24),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Text(title, style: const TextStyle(fontSize: 17, fontWeight: FontWeight.w700)),
          const SizedBox(height: 12),
          ...items,
        ],
      ),
    );
  }
}
