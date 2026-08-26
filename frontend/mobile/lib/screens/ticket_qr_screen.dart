import 'dart:convert';
import 'dart:io';
import 'dart:typed_data';

import 'package:flutter/material.dart';
import 'package:open_filex/open_filex.dart';
import 'package:path_provider/path_provider.dart';

import '../models/responses/ticket_response.dart';
import '../services/api_exception.dart';
import '../services/purchase_service.dart';
import '../theme/app_colors.dart';
import '../widgets/responsive_page.dart';

/// Mockup screen 6 — the QR "ticket stub" card.
///
/// The QR is real and scannable: `ticket.qrImage` is a `data:` PNG rendered
/// server-side by eTicketing.Ticketing, decoded straight into [Image.memory].
/// Rendering it there rather than here means this app needs no QR package, and
/// the code shown is byte-identical to the one in the emailed PDF and the one
/// an organizer's scanner expects.
class TicketQrScreen extends StatelessWidget {
  final TicketResponse ticket;
  final String productName;

  const TicketQrScreen({
    super.key,
    required this.ticket,
    required this.productName,
  });

  String get _validityLine {
    if (ticket.validDate != null) return _formatDate(ticket.validDate!);
    if (ticket.validFrom != null && ticket.validTo != null) {
      return '${_formatDate(ticket.validFrom!)} – ${_formatDate(ticket.validTo!)}';
    }
    return _formatDateTime(ticket.createdAt);
  }

  static String _formatDate(DateTime date) {
    const months = [
      'Jan',
      'Feb',
      'Mar',
      'Apr',
      'Maj',
      'Jun',
      'Jul',
      'Avg',
      'Sep',
      'Okt',
      'Nov',
      'Dec',
    ];
    return '${date.day}. ${months[date.month - 1]} ${date.year}.';
  }

  static String _formatDateTime(DateTime date) {
    final hh = date.hour.toString().padLeft(2, '0');
    final mm = date.minute.toString().padLeft(2, '0');
    return '${_formatDate(date)} · $hh:$mm h';
  }

  /// Strips the `data:image/png;base64,` prefix off the server's data URI.
  /// Returns null if the field is empty or malformed, which the widget below
  /// renders as an honest placeholder rather than a broken image box.
  Uint8List? get _qrBytes {
    final commaIndex = ticket.qrImage.indexOf(',');
    if (commaIndex < 0) return null;
    try {
      return base64Decode(ticket.qrImage.substring(commaIndex + 1));
    } on FormatException {
      return null;
    }
  }

  @override
  Widget build(BuildContext context) {
    final isDark = Theme.of(context).brightness == Brightness.dark;
    final tertiaryText = isDark
        ? AppColors.darkTextTertiary
        : AppColors.lightTextTertiary;

    return Scaffold(
      appBar: AppBar(title: const Text('Ulaznica')),
      body: SafeArea(
        child: SingleChildScrollView(
          padding: const EdgeInsets.all(24),
          child: ResponsivePage(
            padding: EdgeInsets.zero,
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
                            Text(
                              productName,
                              textAlign: TextAlign.center,
                              style: const TextStyle(
                                fontSize: 18,
                                fontWeight: FontWeight.w700,
                                color: Colors.white,
                              ),
                            ),
                            const SizedBox(height: 4),
                            Text(
                              _validityLine,
                              style: const TextStyle(
                                fontSize: 13,
                                color: Colors.white70,
                              ),
                            ),
                          ],
                        ),
                      ),
                      Container(
                        color: isDark ? AppColors.darkSurface : Colors.white,
                        padding: const EdgeInsets.fromLTRB(24, 28, 24, 24),
                        child: Column(
                          children: [
                            _QrPanel(bytes: _qrBytes, isDark: isDark),
                            const SizedBox(height: 8),
                            Text(
                              'Skenirajte na ulazu',
                              style: TextStyle(
                                fontSize: 11,
                                color: tertiaryText,
                              ),
                            ),
                            const SizedBox(height: 14),
                            Text(
                              'Kod ulaznice',
                              style: TextStyle(
                                fontSize: 11,
                                color: tertiaryText,
                              ),
                            ),
                            const SizedBox(height: 4),
                            Text(
                              ticket.id.toUpperCase(),
                              style: const TextStyle(
                                fontSize: 13,
                                fontWeight: FontWeight.w700,
                                fontFamily: 'monospace',
                                letterSpacing: 0.5,
                              ),
                            ),
                            const SizedBox(height: 20),
                            _InfoLine(
                              label: 'Sektor',
                              value: ticket.sectorName,
                            ),
                            if (ticket.ticketTypeName != null)
                              _InfoLine(
                                label: 'Vrsta ulaznice',
                                value: ticket.ticketTypeName!,
                              ),
                            _InfoLine(
                              label: 'Cijena',
                              value:
                                  '${ticket.pricePaid.toStringAsFixed(0)} KM',
                            ),
                            _InfoLine(
                              label: 'Status',
                              value: _statusLabel(ticket.status),
                            ),
                          ],
                        ),
                      ),
                    ],
                  ),
                ),
                const SizedBox(height: 20),
                _DownloadPdfButton(ticket: ticket),
                const SizedBox(height: 12),
                Text(
                  ticket.status == 'Used'
                      ? 'Ova ulaznica je već iskorištena i više ne vrijedi za ulaz.'
                      : 'PDF ulaznica je također poslana na vaš email.',
                  textAlign: TextAlign.center,
                  style: TextStyle(fontSize: 12, color: tertiaryText),
                ),
              ],
            ),
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
    'Used' => 'Iskorištena',
    _ => status,
  };
}

/// The QR plate. Always white-backed regardless of theme: the PNG is pure
/// black-on-white and most scanners expect dark modules on a light field, so
/// letting the dark surface show through would make a valid ticket unreadable
/// at the gate.
class _QrPanel extends StatelessWidget {
  final Uint8List? bytes;
  final bool isDark;

  const _QrPanel({required this.bytes, required this.isDark});

  @override
  Widget build(BuildContext context) {
    final tertiaryText = isDark
        ? AppColors.darkTextTertiary
        : AppColors.lightTextTertiary;

    return Container(
      width: 180,
      height: 180,
      padding: const EdgeInsets.all(8),
      decoration: BoxDecoration(
        color: Colors.white,
        border: Border.all(
          color: isDark ? AppColors.darkBorder : AppColors.lightBorder,
        ),
        borderRadius: BorderRadius.circular(12),
      ),
      child: bytes == null
          ? Center(
              child: Text(
                'QR kod nije dostupan',
                textAlign: TextAlign.center,
                style: TextStyle(fontSize: 11, color: tertiaryText),
              ),
            )
          : Image.memory(
              bytes!,
              // The QR is drawn at a fixed module size server-side; nearest-
              // neighbour keeps the modules crisp when scaled up instead of
              // blurring their edges the way the default filtering does.
              filterQuality: FilterQuality.none,
              fit: BoxFit.contain,
              gaplessPlayback: true,
            ),
    );
  }
}

class _InfoLine extends StatelessWidget {
  final String label;
  final String value;

  const _InfoLine({required this.label, required this.value});

  @override
  Widget build(BuildContext context) {
    final isDark = Theme.of(context).brightness == Brightness.dark;
    final tertiaryText = isDark
        ? AppColors.darkTextTertiary
        : AppColors.lightTextTertiary;
    return Padding(
      padding: const EdgeInsets.only(top: 10),
      child: Row(
        mainAxisAlignment: MainAxisAlignment.spaceBetween,
        children: [
          Text(label, style: TextStyle(fontSize: 13, color: tertiaryText)),
          Text(
            value,
            style: const TextStyle(fontSize: 13, fontWeight: FontWeight.w600),
          ),
        ],
      ),
    );
  }
}

/// "Preuzmi PDF" — saves this ticket's sheet to the device and opens it.
///
/// Owns its own loading state so [TicketQrScreen] can stay stateless. The
/// download is instant by design: eTicketing.Ticketing renders the sheet per
/// request rather than serving a stored file, so there is nothing to queue and
/// nothing to wait for beyond the round-trip.
class _DownloadPdfButton extends StatefulWidget {
  final TicketResponse ticket;

  const _DownloadPdfButton({required this.ticket});

  @override
  State<_DownloadPdfButton> createState() => _DownloadPdfButtonState();
}

class _DownloadPdfButtonState extends State<_DownloadPdfButton> {
  final _purchaseService = PurchaseService();
  bool _isDownloading = false;

  Future<void> _download() async {
    if (_isDownloading) return;
    setState(() => _isDownloading = true);

    try {
      final bytes = await _purchaseService.downloadTicketPdf(widget.ticket.id);

      final directory = await getApplicationDocumentsDirectory();
      final shortId = widget.ticket.id.replaceAll('-', '').substring(0, 8).toUpperCase();
      final file = File('${directory.path}/ulaznica-$shortId.pdf');
      await file.writeAsBytes(bytes, flush: true);

      if (!mounted) return;
      final opened = await OpenFilex.open(file.path);

      if (!mounted) return;
      if (opened.type != ResultType.done) {
        // Saved but nothing on the device can display a PDF — say where it went
        // rather than reporting a failure for something that did work.
        _notify('Ulaznica je sačuvana: ${file.path}');
      }
    } on ApiException catch (e) {
      if (mounted) _notify(e.apiError.displayMessage);
    } catch (_) {
      if (mounted) _notify('Preuzimanje PDF-a nije uspjelo. Pokušajte ponovo.');
    } finally {
      if (mounted) setState(() => _isDownloading = false);
    }
  }

  void _notify(String message) =>
      ScaffoldMessenger.of(context).showSnackBar(SnackBar(content: Text(message)));

  @override
  Widget build(BuildContext context) {
    return SizedBox(
      width: double.infinity,
      child: FilledButton.icon(
        onPressed: _isDownloading ? null : _download,
        icon: _isDownloading
            ? const SizedBox(
                width: 16,
                height: 16,
                child: CircularProgressIndicator(strokeWidth: 2, color: Colors.white),
              )
            : const Icon(Icons.download_rounded, size: 18),
        label: Text(_isDownloading ? 'Preuzimanje…' : 'Preuzmi PDF'),
      ),
    );
  }
}
