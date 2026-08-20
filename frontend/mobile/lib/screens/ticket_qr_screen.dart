import 'package:flutter/material.dart';

import '../models/responses/ticket_response.dart';
import '../theme/app_colors.dart';

/// Mockup screen 6 — the QR "ticket stub" card. The QR itself is a static
/// placeholder (a `qr_code_2` glyph, not a real scannable code and not the
/// `qr_flutter` package) since Ticketing has no real gate-scanning consumer
/// yet — showing a fake-but-scannable-looking pattern would be more
/// misleading than an honest placeholder icon, same principle as
/// `ComingSoonScreen`. The ticket code shown is the real `Ticket.Id`.
class TicketQrScreen extends StatelessWidget {
  final TicketResponse ticket;
  final String productName;

  const TicketQrScreen({super.key, required this.ticket, required this.productName});

  String get _validityLine {
    if (ticket.validDate != null) return _formatDate(ticket.validDate!);
    if (ticket.validFrom != null && ticket.validTo != null) {
      return '${_formatDate(ticket.validFrom!)} – ${_formatDate(ticket.validTo!)}';
    }
    return _formatDateTime(ticket.createdAt);
  }

  static String _formatDate(DateTime date) {
    const months = [
      'Jan', 'Feb', 'Mar', 'Apr', 'Maj', 'Jun', 'Jul', 'Avg', 'Sep', 'Okt', 'Nov', 'Dec',
    ];
    return '${date.day}. ${months[date.month - 1]} ${date.year}.';
  }

  static String _formatDateTime(DateTime date) {
    final hh = date.hour.toString().padLeft(2, '0');
    final mm = date.minute.toString().padLeft(2, '0');
    return '${_formatDate(date)} · $hh:$mm h';
  }

  @override
  Widget build(BuildContext context) {
    final isDark = Theme.of(context).brightness == Brightness.dark;
    final tertiaryText = isDark ? AppColors.darkTextTertiary : AppColors.lightTextTertiary;
    final primary = Theme.of(context).colorScheme.primary;

    return Scaffold(
      appBar: AppBar(title: const Text('Ulaznica')),
      body: SafeArea(
        child: SingleChildScrollView(
          padding: const EdgeInsets.all(24),
          child: Column(
            children: [
              ClipRRect(
                borderRadius: BorderRadius.circular(20),
                child: Column(
                  children: [
                    Container(
                      width: double.infinity,
                      padding: const EdgeInsets.all(24),
                      decoration: const BoxDecoration(
                        gradient: LinearGradient(
                          colors: [AppColors.primary, AppColors.secondary],
                          begin: Alignment.topLeft,
                          end: Alignment.bottomRight,
                        ),
                      ),
                      child: Column(
                        children: [
                          Text(productName, textAlign: TextAlign.center, style: const TextStyle(fontSize: 18, fontWeight: FontWeight.w700, color: Colors.white)),
                          const SizedBox(height: 4),
                          Text(_validityLine, style: const TextStyle(fontSize: 13, color: Colors.white70)),
                        ],
                      ),
                    ),
                    Container(
                      color: isDark ? AppColors.darkSurface : Colors.white,
                      padding: const EdgeInsets.fromLTRB(24, 28, 24, 24),
                      child: Column(
                        children: [
                          Container(
                            width: 160,
                            height: 160,
                            decoration: BoxDecoration(
                              border: Border.all(color: isDark ? AppColors.darkBorder : AppColors.lightBorder),
                              borderRadius: BorderRadius.circular(12),
                            ),
                            child: Icon(Icons.qr_code_2_rounded, size: 120, color: isDark ? AppColors.darkTextPrimary : AppColors.lightTextPrimary),
                          ),
                          const SizedBox(height: 14),
                          Text('Kod ulaznice', style: TextStyle(fontSize: 11, color: tertiaryText)),
                          const SizedBox(height: 4),
                          Text(
                            ticket.id.toUpperCase(),
                            style: const TextStyle(fontSize: 13, fontWeight: FontWeight.w700, fontFamily: 'monospace', letterSpacing: 0.5),
                          ),
                          const SizedBox(height: 20),
                          _InfoLine(label: 'Sektor', value: ticket.sectorName),
                          if (ticket.ticketTypeName != null) _InfoLine(label: 'Vrsta ulaznice', value: ticket.ticketTypeName!),
                          _InfoLine(label: 'Cijena', value: '${ticket.pricePaid.toStringAsFixed(0)} KM'),
                          _InfoLine(label: 'Status', value: _statusLabel(ticket.status)),
                        ],
                      ),
                    ),
                  ],
                ),
              ),
              const SizedBox(height: 20),
              InkWell(
                borderRadius: BorderRadius.circular(12),
                onTap: () => ScaffoldMessenger.of(context).showSnackBar(const SnackBar(content: Text('Nije dostupno.'))),
                child: Container(
                  width: double.infinity,
                  padding: const EdgeInsets.all(14),
                  decoration: BoxDecoration(border: Border.all(color: primary, width: 2), borderRadius: BorderRadius.circular(12)),
                  child: Text('Dodaj u Wallet', textAlign: TextAlign.center, style: TextStyle(color: primary, fontWeight: FontWeight.w700, fontSize: 14)),
                ),
              ),
            ],
          ),
        ),
      ),
    );
  }

  static String _statusLabel(String status) => switch (status) {
        'Confirmed' => 'Potvrđena',
        'Processing' => 'U obradi',
        'Ready' => 'Spremna',
        'Cancelled' => 'Otkazana',
        _ => status,
      };
}

class _InfoLine extends StatelessWidget {
  final String label;
  final String value;

  const _InfoLine({required this.label, required this.value});

  @override
  Widget build(BuildContext context) {
    final isDark = Theme.of(context).brightness == Brightness.dark;
    final tertiaryText = isDark ? AppColors.darkTextTertiary : AppColors.lightTextTertiary;
    return Padding(
      padding: const EdgeInsets.only(top: 10),
      child: Row(
        mainAxisAlignment: MainAxisAlignment.spaceBetween,
        children: [
          Text(label, style: TextStyle(fontSize: 13, color: tertiaryText)),
          Text(value, style: const TextStyle(fontSize: 13, fontWeight: FontWeight.w600)),
        ],
      ),
    );
  }
}
