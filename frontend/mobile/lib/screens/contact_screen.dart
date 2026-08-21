import 'package:flutter/material.dart';

import '../theme/app_colors.dart';
import '../widgets/responsive_page.dart';

/// "Kontakt" — content ported verbatim from `frontend/web`'s
/// `pages/contact/contact.component.html`, for feature parity between the
/// two apps (web had this page, mobile didn't).
class ContactScreen extends StatelessWidget {
  const ContactScreen({super.key});

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      appBar: AppBar(title: const Text('Kontakt')),
      body: SafeArea(
        child: SingleChildScrollView(
          child: ResponsivePage(
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: const [
                Text('Javite nam se, rado ćemo vam pomoći.', style: TextStyle(fontSize: 14)),
                SizedBox(height: 20),
                _ContactCard(icon: Icons.email_outlined, label: 'Email', value: 'info@ekarta.ba'),
                SizedBox(height: 12),
                _ContactCard(icon: Icons.call_outlined, label: 'Telefon', value: '+387 33 123 456'),
                SizedBox(height: 12),
                _ContactCard(icon: Icons.print_outlined, label: 'Fax', value: '+387 33 123 457'),
                SizedBox(height: 12),
                _ContactCard(icon: Icons.location_on_outlined, label: 'Lokacija', value: 'Sarajevo, Grbavica bb'),
              ],
            ),
          ),
        ),
      ),
    );
  }
}

class _ContactCard extends StatelessWidget {
  final IconData icon;
  final String label;
  final String value;

  const _ContactCard({required this.icon, required this.label, required this.value});

  @override
  Widget build(BuildContext context) {
    final isDark = Theme.of(context).brightness == Brightness.dark;
    final tertiaryText = isDark ? AppColors.darkTextTertiary : AppColors.lightTextTertiary;
    final primary = Theme.of(context).colorScheme.primary;
    return Card(
      clipBehavior: Clip.antiAlias,
      child: Padding(
        padding: const EdgeInsets.all(16),
        child: Row(
          children: [
            Container(
              width: 44,
              height: 44,
              decoration: BoxDecoration(
                color: primary.withValues(alpha: 0.1),
                shape: BoxShape.circle,
              ),
              child: Icon(icon, color: primary, size: 20),
            ),
            const SizedBox(width: 14),
            Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Text(label, style: TextStyle(fontSize: 12, color: tertiaryText)),
                const SizedBox(height: 2),
                Text(value, style: const TextStyle(fontSize: 15, fontWeight: FontWeight.w600)),
              ],
            ),
          ],
        ),
      ),
    );
  }
}
