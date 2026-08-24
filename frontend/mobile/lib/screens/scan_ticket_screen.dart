import 'package:flutter/material.dart';
import 'package:mobile_scanner/mobile_scanner.dart';

import '../models/responses/ticket_validation_response.dart';
import '../models/responses/validation_product_response.dart';
import '../services/api_exception.dart';
import '../services/validation_service.dart';
import '../theme/app_colors.dart';

/// The gate. Camera preview scoped to ONE product, and an unmistakable
/// green/red verdict card after every scan.
///
/// Two behaviours here are load-bearing rather than cosmetic:
///
///  * **The camera stops the instant a code is read** and stays stopped while
///    the verdict is on screen. A QR sitting in front of the lens emits a
///    detection every frame, so without this a single ticket would fire dozens
///    of validate calls — the first succeeding and the rest coming back
///    "already used", which is exactly the confusion the feature exists to
///    prevent.
///  * **The verdict is full-bleed and colour-coded**, not a snackbar. Someone
///    working a door glances at the phone for a fraction of a second; a green
///    field means let them in, a red field means stop.
class ScanTicketScreen extends StatefulWidget {
  final ValidationProductResponse product;

  const ScanTicketScreen({super.key, required this.product});

  @override
  State<ScanTicketScreen> createState() => _ScanTicketScreenState();
}

class _ScanTicketScreenState extends State<ScanTicketScreen> {
  final _validationService = ValidationService();
  final _controller = MobileScannerController(
    detectionSpeed: DetectionSpeed.noDuplicates,
    formats: const [BarcodeFormat.qrCode],
  );

  TicketValidationResponse? _result;
  String? _transportError;
  bool _isSubmitting = false;
  int _validatedCount = 0;

  @override
  void initState() {
    super.initState();
    _validatedCount = widget.product.validatedToday;
  }

  @override
  void dispose() {
    _controller.dispose();
    super.dispose();
  }

  Future<void> _onDetect(BarcodeCapture capture) async {
    // Guard on both flags: detections keep arriving for a frame or two after
    // stop() is requested, and any of them would otherwise re-enter this method
    // while the first request is still in flight.
    if (_isSubmitting || _result != null) return;

    final raw = capture.barcodes.firstOrNull?.rawValue;
    if (raw == null || raw.isEmpty) return;

    await _controller.stop();
    await _submit(raw);
  }

  Future<void> _submit(String code) async {
    setState(() {
      _isSubmitting = true;
      _transportError = null;
    });

    try {
      final result = await _validationService.validate(
        productId: widget.product.productId,
        code: code,
      );
      if (!mounted) return;
      setState(() {
        _result = result;
        if (result.isValid) _validatedCount++;
        _isSubmitting = false;
      });
    } on ApiException catch (e) {
      if (!mounted) return;
      setState(() {
        // 403 (not an organizer) and 409 (another device is validating this
        // exact ticket right now) are the only non-200s this endpoint returns.
        // Neither is a statement about the ticket, so neither gets a red
        // "NIJE VALIDNA" card — that would turn away a holder over a race.
        _transportError = e.apiError.displayMessage;
        _isSubmitting = false;
      });
    } catch (_) {
      if (!mounted) return;
      setState(() {
        _transportError = 'Provjera nije uspjela. Provjerite vezu i pokušajte ponovo.';
        _isSubmitting = false;
      });
    }
  }

  void _scanNext() {
    // Deliberately does NOT call _controller.start(). Clearing the verdict
    // remounts MobileScanner, whose initState starts the controller itself
    // (autoStart defaults to true) — and MobileScannerController.start()
    // throws `controllerInitializing` if it's called while a start is already
    // in flight, which is exactly the race a second call here would create.
    setState(() {
      _result = null;
      _transportError = null;
    });
  }

  Future<void> _enterManually() async {
    final controller = TextEditingController();

    try {
      await _promptForCode(controller);
    } finally {
      controller.dispose();
    }
  }

  Future<void> _promptForCode(TextEditingController controller) async {
    final code = await showDialog<String>(
      context: context,
      builder: (context) => AlertDialog(
        title: const Text('Unesi kod ručno'),
        content: TextField(
          controller: controller,
          autofocus: true,
          decoration: const InputDecoration(
            labelText: 'Kod ulaznice',
            hintText: 'npr. 3f2a9c1e-...',
          ),
          // Server-side rule mirrored here (ValidateTicketRequestValidator):
          // the code is capped at 200 characters.
          maxLength: 200,
        ),
        actions: [
          TextButton(onPressed: () => Navigator.of(context).pop(), child: const Text('Odustani')),
          FilledButton(
            onPressed: () => Navigator.of(context).pop(controller.text.trim()),
            child: const Text('Provjeri'),
          ),
        ],
      ),
    );

    if (code == null || code.isEmpty) return;

    await _controller.stop();
    await _submit(code);
  }

  @override
  Widget build(BuildContext context) {
    final hasVerdict = _result != null || _transportError != null;

    return Scaffold(
      appBar: AppBar(
        title: Text(widget.product.name, overflow: TextOverflow.ellipsis),
        actions: [
          IconButton(
            onPressed: _enterManually,
            icon: const Icon(Icons.keyboard_rounded),
            tooltip: 'Unesi kod ručno',
          ),
        ],
      ),
      body: SafeArea(
        child: Column(
          children: [
            _CounterBar(validated: _validatedCount, total: widget.product.totalToday),
            Expanded(
              child: hasVerdict
                  ? _VerdictCard(
                      result: _result,
                      transportError: _transportError,
                      onScanNext: _scanNext,
                    )
                  : _ScannerView(controller: _controller, onDetect: _onDetect, isSubmitting: _isSubmitting),
            ),
          ],
        ),
      ),
    );
  }
}

class _CounterBar extends StatelessWidget {
  final int validated;
  final int total;

  const _CounterBar({required this.validated, required this.total});

  @override
  Widget build(BuildContext context) {
    final isDark = Theme.of(context).brightness == Brightness.dark;

    return Container(
      width: double.infinity,
      padding: const EdgeInsets.symmetric(horizontal: 20, vertical: 12),
      color: isDark ? AppColors.darkSurfaceMuted : const Color(0xFFF5F5F5),
      child: Text(
        'Validirano danas: $validated / $total',
        textAlign: TextAlign.center,
        style: const TextStyle(fontSize: 13, fontWeight: FontWeight.w600),
      ),
    );
  }
}

class _ScannerView extends StatelessWidget {
  final MobileScannerController controller;
  final Future<void> Function(BarcodeCapture) onDetect;
  final bool isSubmitting;

  const _ScannerView({
    required this.controller,
    required this.onDetect,
    required this.isSubmitting,
  });

  @override
  Widget build(BuildContext context) {
    return Stack(
      fit: StackFit.expand,
      children: [
        MobileScanner(
          controller: controller,
          onDetect: onDetect,
          // Camera denied, unavailable, or the platform has none — say so
          // instead of leaving a black rectangle, and point at the manual
          // fallback that still works.
          errorBuilder: (context, error) => const _ScannerUnavailable(),
        ),
        // Aiming frame — purely a targeting aid, so it must never eat the taps
        // the preview underneath needs.
        IgnorePointer(
          child: Center(
            child: Container(
              width: 240,
              height: 240,
              decoration: BoxDecoration(
                border: Border.all(color: Colors.white, width: 3),
                borderRadius: BorderRadius.circular(16),
              ),
            ),
          ),
        ),
        Positioned(
          left: 0,
          right: 0,
          bottom: 32,
          child: Text(
            isSubmitting ? 'Provjeravam…' : 'Usmjerite kameru na QR kod ulaznice',
            textAlign: TextAlign.center,
            style: const TextStyle(
              color: Colors.white,
              fontSize: 13,
              fontWeight: FontWeight.w600,
              shadows: [Shadow(blurRadius: 6, color: Colors.black54)],
            ),
          ),
        ),
        if (isSubmitting)
          const ColoredBox(
            color: Colors.black45,
            child: Center(child: CircularProgressIndicator(color: Colors.white)),
          ),
      ],
    );
  }
}

class _ScannerUnavailable extends StatelessWidget {
  const _ScannerUnavailable();

  @override
  Widget build(BuildContext context) {
    return const Center(
      child: Padding(
        padding: EdgeInsets.all(32),
        child: Column(
          mainAxisSize: MainAxisSize.min,
          children: [
            Icon(Icons.no_photography_rounded, size: 48),
            SizedBox(height: 16),
            Text(
              'Kamera nije dostupna. Provjerite dozvolu za kameru ili unesite kod ručno.',
              textAlign: TextAlign.center,
            ),
          ],
        ),
      ),
    );
  }
}

/// Green VALIDNA or red NIJE VALIDNA, filling the screen. [transportError]
/// gets its own neutral amber treatment: a 403/409 says nothing about the
/// ticket, so it must not read as a rejection.
class _VerdictCard extends StatelessWidget {
  final TicketValidationResponse? result;
  final String? transportError;
  final VoidCallback onScanNext;

  const _VerdictCard({
    required this.result,
    required this.transportError,
    required this.onScanNext,
  });

  @override
  Widget build(BuildContext context) {
    final isError = transportError != null;
    final isValid = result?.isValid ?? false;

    final (background, icon, headline) = switch ((isError, isValid)) {
      (true, _) => (const Color(0xFFB45309), Icons.info_outline_rounded, 'PONOVITE PROVJERU'),
      (false, true) => (const Color(0xFF15803D), Icons.check_circle_rounded, 'VALIDNA'),
      (false, false) => (const Color(0xFFB91C1C), Icons.cancel_rounded, 'NIJE VALIDNA'),
    };

    return Container(
      color: background,
      width: double.infinity,
      child: SingleChildScrollView(
        padding: const EdgeInsets.all(24),
        child: Column(
          mainAxisAlignment: MainAxisAlignment.center,
          children: [
            const SizedBox(height: 24),
            Icon(icon, size: 96, color: Colors.white),
            const SizedBox(height: 12),
            Text(
              headline,
              textAlign: TextAlign.center,
              style: const TextStyle(
                fontSize: 30,
                fontWeight: FontWeight.w800,
                color: Colors.white,
                letterSpacing: 1.5,
              ),
            ),
            const SizedBox(height: 12),
            Text(
              transportError ?? result!.message,
              textAlign: TextAlign.center,
              style: const TextStyle(fontSize: 15, color: Colors.white),
            ),
            if (result != null) ...[
              const SizedBox(height: 20),
              _TicketDetails(result: result!),
            ],
            const SizedBox(height: 28),
            SizedBox(
              width: double.infinity,
              child: FilledButton(
                onPressed: onScanNext,
                style: FilledButton.styleFrom(
                  backgroundColor: Colors.white,
                  foregroundColor: background,
                  padding: const EdgeInsets.symmetric(vertical: 16),
                ),
                child: const Text(
                  'Skeniraj sljedeću',
                  style: TextStyle(fontWeight: FontWeight.w700, fontSize: 15),
                ),
              ),
            ),
            const SizedBox(height: 24),
          ],
        ),
      ),
    );
  }
}

class _TicketDetails extends StatelessWidget {
  final TicketValidationResponse result;

  const _TicketDetails({required this.result});

  static String _formatDateTime(DateTime value) {
    final local = value.toLocal();
    final dd = local.day.toString().padLeft(2, '0');
    final mo = local.month.toString().padLeft(2, '0');
    final hh = local.hour.toString().padLeft(2, '0');
    final mi = local.minute.toString().padLeft(2, '0');
    return '$dd.$mo.${local.year}. $hh:$mi';
  }

  @override
  Widget build(BuildContext context) {
    final rows = <(String, String)>[
      if (result.sectorName != null) ('Sektor', result.sectorName!),
      if (result.ticketTypeName != null) ('Vrsta ulaznice', result.ticketTypeName!),
      if (result.holderEmail != null) ('Kupac', result.holderEmail!),
      // Only meaningful on the "already used" path, where it tells the person
      // at the door when this ticket was let through the first time.
      if (result.validatedAt != null) ('Validirano', _formatDateTime(result.validatedAt!)),
      if (result.ticketId != null) ('Kod', result.ticketId!.toUpperCase()),
    ];

    if (rows.isEmpty) return const SizedBox.shrink();

    return Container(
      width: double.infinity,
      padding: const EdgeInsets.all(16),
      decoration: BoxDecoration(
        color: Colors.white24,
        borderRadius: BorderRadius.circular(12),
      ),
      child: Column(
        children: [
          for (final (label, value) in rows)
            Padding(
              padding: const EdgeInsets.only(bottom: 8),
              child: Row(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  Text(label, style: const TextStyle(fontSize: 12, color: Colors.white70)),
                  const SizedBox(width: 12),
                  Expanded(
                    child: Text(
                      value,
                      textAlign: TextAlign.right,
                      style: const TextStyle(
                        fontSize: 12,
                        fontWeight: FontWeight.w600,
                        color: Colors.white,
                      ),
                    ),
                  ),
                ],
              ),
            ),
        ],
      ),
    );
  }
}
