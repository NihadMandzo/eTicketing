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
import '../utils/ticket_validity.dart';
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

  /// When the thing this ticket admits you to actually happens. A one-off event ticket carries no
  /// date of its own — the showing date lives on `Product.Date` — so the caller supplies it.
  final DateTime? eventDate;

  /// The last date the ticket is good for, which is the same as [eventDate] except for a
  /// subscription, where it is the end of the period rather than its start. Feeds
  /// [resolveTicketValidity].
  final DateTime? expiresAfter;

  const TicketQrScreen({
    super.key,
    required this.ticket,
    required this.productName,
    this.eventDate,
    this.expiresAfter,
  });

  TicketValidity get _validity =>
      resolveTicketValidity(status: ticket.status, expiresAfter: expiresAfter ?? eventDate);

  /// When this ticket is for. Never when it was bought.
  ///
  /// This used to fall back to `_formatDateTime(ticket.createdAt)`, so a ticket to a concert next
  /// month announced the afternoon the card was charged, in the most prominent line on the screen
  /// — directly under the event name, where a reader takes it for the event's own date. Now the
  /// line is simply omitted when there is no real date to state.
  String? get _whenLine {
    if (ticket.validDate != null) return _formatDate(ticket.validDate!);
    if (ticket.validFrom != null && ticket.validTo != null) {
      return '${_formatDate(ticket.validFrom!)} – ${_formatDate(ticket.validTo!)}';
    }
    return eventDate == null ? null : _formatDateTime(eventDate!);
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
    final validity = _validity;
    final isUsable = validity.isUsable;

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
                        // The brand gradient is reserved for a ticket that still works. A spent or
                        // cancelled one gets flat grey, so the difference is visible before a
                        // single word is read — the same rule the list's date blocks follow.
                        decoration: BoxDecoration(
                          gradient: isUsable
                              ? const LinearGradient(
                                  colors: [AppColors.primary, AppColors.secondary],
                                  begin: Alignment.topLeft,
                                  end: Alignment.bottomRight,
                                )
                              : null,
                          color: isUsable
                              ? null
                              : (isDark ? AppColors.darkSurfaceMuted : AppColors.lightSurfaceMuted),
                        ),
                        child: Column(
                          children: [
                            Text(
                              productName,
                              textAlign: TextAlign.center,
                              style: TextStyle(
                                fontSize: 18,
                                fontWeight: FontWeight.w700,
                                color: isUsable
                                    ? Colors.white
                                    : (isDark ? AppColors.darkTextPrimary : AppColors.lightTextPrimary),
                              ),
                            ),
                            const SizedBox(height: 10),
                            TicketValidityBadge(validity: validity, prominent: true),
                            if (_whenLine != null) ...[
                              const SizedBox(height: 10),
                              Text(
                                _whenLine!,
                                textAlign: TextAlign.center,
                                style: TextStyle(
                                  fontSize: 13,
                                  color: isUsable ? Colors.white70 : tertiaryText,
                                ),
                              ),
                            ],
                          ],
                        ),
                      ),
                      Container(
                        color: isDark ? AppColors.darkSurface : Colors.white,
                        padding: const EdgeInsets.fromLTRB(24, 28, 24, 24),
                        child: Column(
                          children: [
                            _QrPanel(bytes: _qrBytes, isDark: isDark, isUsable: isUsable),
                            const SizedBox(height: 8),
                            Text(
                              isUsable ? 'Skenirajte na ulazu' : 'Ovaj kod više ne vrijedi za ulaz',
                              textAlign: TextAlign.center,
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
                            if (eventDate != null && ticket.validDate == null && ticket.validFrom == null)
                              _InfoLine(label: 'Datum događaja', value: _formatDateTime(eventDate!)),
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
                  switch (validity) {
                    TicketValidity.used => 'Ova ulaznica je već iskorištena i više ne vrijedi za ulaz.',
                    TicketValidity.cancelled => 'Ova ulaznica je otkazana i ne vrijedi za ulaz.',
                    TicketValidity.expired => 'Termin je prošao, pa ova ulaznica više ne vrijedi za ulaz.',
                    TicketValidity.pending => 'Plaćanje se još obrađuje. Ulaznica vrijedi tek kad bude potvrđena.',
                    TicketValidity.valid => 'PDF ulaznica je također poslana na vaš email.',
                  },
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

  /// A spent, cancelled or expired code is faded and struck with its state.
  ///
  /// The plate stays white and the modules stay untouched underneath — this is a scrim over the
  /// top, not a redraw — because the code is still the real signed payload and an organizer may
  /// well want to scan it precisely to see why it was refused. What the fade prevents is a holder
  /// walking up to a gate believing this one will open it.
  final bool isUsable;

  const _QrPanel({required this.bytes, required this.isDark, this.isUsable = true});

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
          color: isUsable
              ? (isDark ? AppColors.darkBorder : AppColors.lightBorder)
              : AppColors.lightTextDisabled,
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
          : Stack(
              fit: StackFit.expand,
              children: [
                Opacity(
                  opacity: isUsable ? 1 : 0.18,
                  child: Image.memory(
                    bytes!,
                    // The QR is drawn at a fixed module size server-side; nearest-
                    // neighbour keeps the modules crisp when scaled up instead of
                    // blurring their edges the way the default filtering does.
                    filterQuality: FilterQuality.none,
                    fit: BoxFit.contain,
                    gaplessPlayback: true,
                  ),
                ),
                if (!isUsable)
                  Center(
                    child: Transform.rotate(
                      angle: -0.18,
                      child: Container(
                        padding: const EdgeInsets.symmetric(horizontal: 12, vertical: 6),
                        decoration: BoxDecoration(
                          border: Border.all(color: AppColors.lightTextTertiary, width: 2),
                          borderRadius: BorderRadius.circular(6),
                        ),
                        child: const Text(
                          'NE VRIJEDI',
                          style: TextStyle(
                            fontSize: 14,
                            fontWeight: FontWeight.w800,
                            letterSpacing: 1.5,
                            color: AppColors.lightTextTertiary,
                          ),
                        ),
                      ),
                    ),
                  ),
              ],
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
      if (bytes.isEmpty) {
        // An unexpected empty response body — writing it would produce a broken 0-byte .pdf that
        // still looks like a successful download. Treat it as the same failure as a network error.
        throw Exception('Empty PDF response');
      }

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
