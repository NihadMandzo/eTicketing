import 'dart:async';

import 'package:flutter/material.dart';
import 'package:lucide_icons_flutter/lucide_icons.dart';

import '../core/export_notifications.dart';
import '../main.dart';
import '../models/enums/ticketing_mode.dart';
import '../models/requests/create_ticket_print_batch_request.dart';
import '../models/responses/product_response.dart';
import '../models/responses/ticket_print_batch_response.dart';
import '../models/responses/ticket_print_options_response.dart';
import '../providers/ticket_print_provider.dart';
import '../theme/app_colors.dart';
import 'widgets/ticket_export_download.dart';

/// Izvoz fizičkih ulaznica — the box-office side of a product.
///
/// The organizer picks sectors and how many tickets of each price tier to
/// print; submitting mints real, gate-valid tickets and claims their capacity,
/// then queues the A4 sheet for rendering. Rendering is asynchronous because a
/// batch can run to thousands of tickets, so the primary button walks through
/// four states — export, preparing, download, retry — and the organizer is free
/// to leave the screen at any point (the top-bar badge picks the batch back up).
///
/// Downloading destroys the server's copy, so after a successful save the whole
/// screen resets to a fresh selection with refreshed remaining-capacity figures.
class TicketExportScreen extends StatefulWidget {
  final ProductResponse product;

  const TicketExportScreen({super.key, required this.product});

  @override
  State<TicketExportScreen> createState() => _TicketExportScreenState();
}

class _TicketExportScreenState extends State<TicketExportScreen> {
  static const _presets = [25, 50, 100, 250];
  static const _pollInterval = Duration(seconds: 2);

  final _provider = TicketPrintProvider();

  TicketPrintOptionsResponse? _options;
  bool _isLoading = true;
  String? _loadError;

  /// Which sectors are switched on, and how many tickets of each
  /// `sectorId|ticketTypeId` line the organizer wants.
  final Set<String> _enabledSectors = {};
  final Map<String, int> _quantities = {};

  DateTime? _validDate;

  TicketPrintBatchResponse? _batch;
  Timer? _pollTimer;
  bool _isSubmitting = false;
  bool _isDownloading = false;

  bool get _isDark => Theme.of(context).brightness == Brightness.dark;

  @override
  void initState() {
    super.initState();
    _load();
  }

  @override
  void dispose() {
    _pollTimer?.cancel();
    super.dispose();
  }

  // ------------------------------------------------------------------ loading

  Future<void> _load() async {
    setState(() {
      _isLoading = true;
      _loadError = null;
    });

    try {
      final options = await _provider.getOptions(widget.product.id, date: _validDate);
      // Picks up a render started before the organizer navigated away, so
      // coming back to this product shows the batch instead of a blank form.
      final latest = await _provider.getLatestForProduct(widget.product.id);

      if (!mounted) return;
      setState(() {
        _options = options;
        _isLoading = false;
        if (latest != null && (latest.status.isInFlight || latest.isDownloadable)) {
          _batch = latest;
        }
        for (final sector in options.sectors) {
          _enabledSectors.add(sector.sectorId);
        }
      });

      if (_batch?.status.isInFlight ?? false) _startPolling();
    } catch (e) {
      if (!mounted) return;
      setState(() {
        _isLoading = false;
        _loadError = e.toString();
      });
      handleApiError(e);
    }
  }

  /// Re-reads remaining capacity without disturbing the current selection —
  /// used after picking a DailyEntry date, where capacity is per calendar day.
  Future<void> _refreshOptions() async {
    try {
      final options = await _provider.getOptions(widget.product.id, date: _validDate);
      if (!mounted) return;
      setState(() => _options = options);
    } catch (e) {
      handleApiError(e);
    }
  }

  // ------------------------------------------------------------- selection

  String _lineKey(String sectorId, String? ticketTypeId) => '$sectorId|${ticketTypeId ?? ''}';

  int _quantityOf(String sectorId, String? ticketTypeId) =>
      _quantities[_lineKey(sectorId, ticketTypeId)] ?? 0;

  /// Total requested for one sector across all its price tiers.
  int _sectorTotal(TicketPrintSectorOption sector) {
    if (!_enabledSectors.contains(sector.sectorId)) return 0;
    if (sector.ticketTypes.isEmpty) return _quantityOf(sector.sectorId, null);
    return sector.ticketTypes.fold(0, (sum, t) => sum + _quantityOf(sector.sectorId, t.id));
  }

  bool _isOverCapacity(TicketPrintSectorOption sector) => _sectorTotal(sector) > sector.remaining;

  int get _totalTickets =>
      (_options?.sectors ?? const <TicketPrintSectorOption>[]).fold(0, (sum, s) => sum + _sectorTotal(s));

  double get _totalValue {
    var total = 0.0;
    for (final sector in _options?.sectors ?? const <TicketPrintSectorOption>[]) {
      if (!_enabledSectors.contains(sector.sectorId)) continue;
      if (sector.ticketTypes.isEmpty) {
        total += _quantityOf(sector.sectorId, null) * sector.price;
      } else {
        for (final type in sector.ticketTypes) {
          total += _quantityOf(sector.sectorId, type.id) * type.price;
        }
      }
    }
    return total;
  }

  int get _totalPages {
    final perSheet = _options?.ticketsPerSheet ?? 3;
    return (_totalTickets / perSheet).ceil();
  }

  /// The one blocking rule the user asked for by name: never more tickets than
  /// the sector has room for. Mirrors the backend's `print.capacity_exceeded`,
  /// which re-checks atomically regardless.
  String? get _capacityError {
    for (final sector in _options?.sectors ?? const <TicketPrintSectorOption>[]) {
      if (_isOverCapacity(sector)) {
        return 'Broj karata za sektor "${sector.name}" premašuje preostali kapacitet (${sector.remaining}).';
      }
    }
    return null;
  }

  String? get _validationError {
    final options = _options;
    if (options == null) return null;
    if (!options.canExport) return options.blockedReason;
    if (options.ticketingMode == TicketingMode.dailyEntry && _validDate == null) {
      return 'Odaberite datum za koji ulaznice važe.';
    }
    if (_totalTickets == 0) return null;
    if (_totalTickets > options.maxTicketsPerBatch) {
      return 'Jedan izvoz može sadržavati najviše ${options.maxTicketsPerBatch} ulaznica.';
    }
    return _capacityError;
  }

  bool get _canSubmit =>
      _totalTickets > 0 && _validationError == null && !_isSubmitting && (_options?.canExport ?? false);

  void _setQuantity(String sectorId, String? ticketTypeId, int value) {
    final sector = _options?.sectors.firstWhere((s) => s.sectorId == sectorId);
    // Clamped at the sector's remaining capacity so the stepper can never walk
    // past what the backend will accept.
    final ceiling = sector?.remaining ?? 0;
    setState(() => _quantities[_lineKey(sectorId, ticketTypeId)] = value.clamp(0, ceiling));
  }

  void _resetSelection() {
    setState(() {
      _quantities.clear();
      for (final sector in _options?.sectors ?? const <TicketPrintSectorOption>[]) {
        _enabledSectors.add(sector.sectorId);
      }
    });
  }

  // ---------------------------------------------------------------- the batch

  Future<void> _submit() async {
    if (!_canSubmit) return;
    setState(() => _isSubmitting = true);

    final lines = <TicketPrintLineRequest>[];
    for (final sector in _options!.sectors) {
      if (!_enabledSectors.contains(sector.sectorId)) continue;

      if (sector.ticketTypes.isEmpty) {
        final quantity = _quantityOf(sector.sectorId, null);
        if (quantity > 0) lines.add(TicketPrintLineRequest(sectorId: sector.sectorId, quantity: quantity));
      } else {
        for (final type in sector.ticketTypes) {
          final quantity = _quantityOf(sector.sectorId, type.id);
          if (quantity > 0) {
            lines.add(TicketPrintLineRequest(
                sectorId: sector.sectorId, ticketTypeId: type.id, quantity: quantity));
          }
        }
      }
    }

    try {
      final batch = await _provider.create(CreateTicketPrintBatchRequest(
        productId: widget.product.id,
        validDate: _validDate,
        lines: lines,
      ));

      if (!mounted) return;
      setState(() {
        _batch = batch;
        _isSubmitting = false;
      });

      handleApiSuccess('Ulaznice su izdate. PDF se priprema — možete nastaviti s radom.');
      exportNotifications.refresh();
      _startPolling();
    } catch (e) {
      if (mounted) setState(() => _isSubmitting = false);
      handleApiError(e);
      // Capacity may have moved under us — re-read it so the form reflects
      // what is actually left.
      await _refreshOptions();
    }
  }

  void _startPolling() {
    _pollTimer?.cancel();
    _pollTimer = Timer.periodic(_pollInterval, (_) => _poll());
  }

  Future<void> _poll() async {
    final batch = _batch;
    if (batch == null) return;

    try {
      final updated = await _provider.getBatch(batch.id);
      if (!mounted) return;

      setState(() => _batch = updated);

      if (!updated.status.isInFlight) {
        _pollTimer?.cancel();
        exportNotifications.refresh();
      }
    } catch (_) {
      // A single failed poll is not worth a snackbar — the next tick retries,
      // and a genuinely broken session is handled by the api client's own 401
      // interceptor.
    }
  }

  Future<void> _download() async {
    final batch = _batch;
    if (batch == null || _isDownloading) return;

    setState(() => _isDownloading = true);
    final saved = await saveTicketExport(context, batch, provider: _provider);
    if (!mounted) return;

    if (!saved) {
      setState(() => _isDownloading = false);
      return;
    }

    // The server destroyed its copy as it handed the bytes over, so this batch
    // is finished with — clear it and start over with fresh capacity figures.
    setState(() {
      _batch = null;
      _isDownloading = false;
      _quantities.clear();
    });
    await _refreshOptions();
  }

  Future<void> _retry() async {
    final batch = _batch;
    if (batch == null) return;

    try {
      final updated = await _provider.retry(batch.id);
      if (!mounted) return;
      setState(() => _batch = updated);
      _startPolling();
    } catch (e) {
      handleApiError(e);
    }
  }

  Future<void> _pickDate() async {
    final sector = _options?.sectors.firstOrNull;
    final year = sector?.periodYear ?? DateTime.now().year;
    final month = sector?.periodMonth ?? DateTime.now().month;
    final first = DateTime(year, month, 1);
    final last = DateTime(year, month + 1, 0);

    final picked = await showDatePicker(
      context: context,
      initialDate: _validDate ?? (DateTime.now().isAfter(first) && DateTime.now().isBefore(last) ? DateTime.now() : first),
      firstDate: first,
      lastDate: last,
      helpText: 'Datum za koji ulaznice važe',
      cancelText: 'Otkaži',
      confirmText: 'Potvrdi',
    );

    if (picked == null) return;
    setState(() => _validDate = picked);
    await _refreshOptions();
  }

  // -------------------------------------------------------------------- build

  @override
  Widget build(BuildContext context) {
    final isDark = _isDark;
    final textPrimary = isDark ? AppColors.darkTextPrimary : AppColors.lightTextPrimary;
    final textTertiary = isDark ? AppColors.darkTextTertiary : AppColors.lightTextTertiary;

    return Scaffold(
      backgroundColor: isDark ? AppColors.darkBackground : AppColors.lightBackground,
      appBar: AppBar(
        backgroundColor: isDark ? AppColors.darkSurface : Colors.white,
        foregroundColor: textPrimary,
        elevation: 0,
        titleSpacing: 0,
        title: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          mainAxisAlignment: MainAxisAlignment.center,
          children: [
            const Text('Izvoz fizičkih ulaznica',
                style: TextStyle(fontSize: 18, fontWeight: FontWeight.w700)),
            Text(
              '${widget.product.name} — spremno za štampu na A4 papiru',
              style: TextStyle(fontSize: 12, fontWeight: FontWeight.w400, color: textTertiary),
              overflow: TextOverflow.ellipsis,
            ),
          ],
        ),
        actions: [
          Padding(
            padding: const EdgeInsets.symmetric(horizontal: 16),
            child: _buildPrimaryAction(),
          ),
        ],
      ),
      body: _isLoading
          ? const Center(child: CircularProgressIndicator())
          : _loadError != null
              ? _buildLoadError(textTertiary)
              : LayoutBuilder(
                  builder: (context, constraints) {
                    // Side by side while there is room for a 360px panel plus a
                    // usable preview; stacked below that, same breakpoint style
                    // as the rest of the app.
                    if (constraints.maxWidth >= 900) {
                      return Row(
                        crossAxisAlignment: CrossAxisAlignment.start,
                        children: [
                          SizedBox(
                            width: 380,
                            child: SingleChildScrollView(
                              padding: const EdgeInsets.all(20),
                              child: _buildSelectionPanel(),
                            ),
                          ),
                          Expanded(
                            child: SingleChildScrollView(
                              padding: const EdgeInsets.all(20),
                              child: _buildPreviewColumn(),
                            ),
                          ),
                        ],
                      );
                    }

                    return SingleChildScrollView(
                      padding: const EdgeInsets.all(20),
                      child: Column(
                        crossAxisAlignment: CrossAxisAlignment.stretch,
                        children: [
                          _buildSelectionPanel(),
                          const SizedBox(height: 20),
                          _buildPreviewColumn(),
                        ],
                      ),
                    );
                  },
                ),
    );
  }

  Widget _buildLoadError(Color textTertiary) => Center(
        child: Padding(
          padding: const EdgeInsets.all(32),
          child: Column(
            mainAxisSize: MainAxisSize.min,
            children: [
              Icon(LucideIcons.circleAlert, size: 32, color: textTertiary),
              const SizedBox(height: 12),
              Text('Podaci za izvoz nisu učitani.',
                  style: TextStyle(color: textTertiary), textAlign: TextAlign.center),
              const SizedBox(height: 12),
              OutlinedButton(onPressed: _load, child: const Text('Pokušaj ponovo')),
            ],
          ),
        ),
      );

  // ------------------------------------------------------- the primary button

  /// The four states the user asked to be visually distinct: export → preparing
  /// → download → retry.
  Widget _buildPrimaryAction() {
    final batch = _batch;

    if (batch != null && batch.status == TicketPrintBatchStatus.failed) {
      return FilledButton.icon(
        onPressed: _retry,
        style: FilledButton.styleFrom(backgroundColor: AppColors.error),
        icon: const Icon(LucideIcons.rotateCw, size: 16),
        label: const Text('Pokušaj ponovo'),
      );
    }

    if (batch != null && batch.isDownloadable) {
      return FilledButton.icon(
        onPressed: _isDownloading ? null : _download,
        style: FilledButton.styleFrom(backgroundColor: AppColors.successDark),
        icon: _isDownloading
            ? const SizedBox(
                width: 16, height: 16, child: CircularProgressIndicator(strokeWidth: 2, color: Colors.white))
            : const Icon(LucideIcons.download, size: 16),
        label: Text(_isDownloading ? 'Preuzimanje…' : 'Preuzmi PDF (${batch.ticketCount} karata)'),
      );
    }

    if (batch != null && batch.status.isInFlight) {
      final progress = batch.ticketCount == 0
          ? ''
          : ' ${batch.renderedCount}/${batch.ticketCount}';
      return FilledButton.icon(
        onPressed: null,
        icon: const SizedBox(width: 16, height: 16, child: CircularProgressIndicator(strokeWidth: 2)),
        label: Text('Priprema PDF-a…$progress'),
      );
    }

    return FilledButton.icon(
      onPressed: _canSubmit ? _submit : null,
      icon: _isSubmitting
          ? const SizedBox(
              width: 16, height: 16, child: CircularProgressIndicator(strokeWidth: 2, color: Colors.white))
          : const Icon(LucideIcons.fileDown, size: 16),
      label: Text(_totalTickets > 0 ? 'Izvezi $_totalTickets karata (PDF)' : 'Izvezi PDF'),
    );
  }

  // ------------------------------------------------------- selection panel

  Widget _buildSelectionPanel() {
    final isDark = _isDark;
    final options = _options!;
    final surface = isDark ? AppColors.darkSurface : Colors.white;
    final border = isDark ? AppColors.darkBorder : AppColors.lightBorder;
    final textTertiary = isDark ? AppColors.darkTextTertiary : AppColors.lightTextTertiary;

    return Container(
      padding: const EdgeInsets.all(18),
      decoration: BoxDecoration(
        color: surface,
        border: Border.all(color: border),
        borderRadius: BorderRadius.circular(16),
      ),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          const Text('Odaberite ulaznice', style: TextStyle(fontSize: 15, fontWeight: FontWeight.w700)),
          const SizedBox(height: 2),
          Text('Označite sektore i unesite broj karata po tipu.',
              style: TextStyle(fontSize: 12, color: textTertiary)),
          const SizedBox(height: 16),

          if (!options.canExport) ...[
            _InfoBanner(
              icon: LucideIcons.circleAlert,
              color: AppColors.warningDark,
              text: options.blockedReason ?? 'Izvoz nije moguć za ovaj proizvod.',
            ),
            const SizedBox(height: 16),
          ],

          if (options.ticketingMode == TicketingMode.dailyEntry) ...[
            _buildDatePickerRow(border, textTertiary),
            const SizedBox(height: 14),
          ],

          for (final sector in options.sectors) ...[
            _buildSectorCard(sector),
            const SizedBox(height: 12),
          ],

          if (options.sectors.isNotEmpty) ...[
            Divider(color: border),
            const SizedBox(height: 6),
            _SummaryRow(label: 'Ukupno karata', value: '$_totalTickets'),
            const SizedBox(height: 8),
            _SummaryRow(
              label: 'Nominalna vrijednost',
              value: _formatMoney(_totalValue),
              valueColor: AppColors.successDark,
            ),
            const SizedBox(height: 8),
            _SummaryRow(
              label: 'A4 stranica (${options.ticketsPerSheet} po strani)',
              value: '$_totalPages',
            ),
            const SizedBox(height: 14),
            Row(
              children: [
                Expanded(
                  child: Text(
                    'Numeracija počinje od #${options.nextSerialNumber.toString().padLeft(6, '0')}',
                    style: TextStyle(fontSize: 12, color: textTertiary),
                  ),
                ),
                const SizedBox(width: 8),
                OutlinedButton(
                  onPressed: _resetSelection,
                  style: OutlinedButton.styleFrom(
                    padding: const EdgeInsets.symmetric(horizontal: 12, vertical: 8),
                    minimumSize: Size.zero,
                    tapTargetSize: MaterialTapTargetSize.shrinkWrap,
                  ),
                  child: const Text('Poništi', style: TextStyle(fontSize: 12)),
                ),
              ],
            ),
          ],

          if (_validationError != null) ...[
            const SizedBox(height: 14),
            _InfoBanner(icon: LucideIcons.triangleAlert, color: AppColors.error, text: _validationError!),
          ],
        ],
      ),
    );
  }

  Widget _buildDatePickerRow(Color border, Color textTertiary) {
    final label = _validDate == null
        ? 'Odaberite datum'
        : '${_validDate!.day.toString().padLeft(2, '0')}.${_validDate!.month.toString().padLeft(2, '0')}.${_validDate!.year}.';

    return InkWell(
      borderRadius: BorderRadius.circular(10),
      onTap: _pickDate,
      child: Container(
        padding: const EdgeInsets.symmetric(horizontal: 12, vertical: 10),
        decoration: BoxDecoration(
          border: Border.all(color: border),
          borderRadius: BorderRadius.circular(10),
        ),
        child: Row(
          children: [
            Icon(LucideIcons.calendarDays, size: 16, color: textTertiary),
            const SizedBox(width: 10),
            Expanded(
              child: Text('Ulaznice važe za: $label',
                  style: const TextStyle(fontSize: 13, fontWeight: FontWeight.w600)),
            ),
          ],
        ),
      ),
    );
  }

  Widget _buildSectorCard(TicketPrintSectorOption sector) {
    final isDark = _isDark;
    final enabled = _enabledSectors.contains(sector.sectorId);
    final over = _isOverCapacity(sector);
    final total = _sectorTotal(sector);
    final primary = isDark ? AppColors.secondary : AppColors.primary;
    final border = isDark ? AppColors.darkBorder : AppColors.lightBorder;
    final textTertiary = isDark ? AppColors.darkTextTertiary : AppColors.lightTextTertiary;

    return Container(
      padding: const EdgeInsets.all(14),
      decoration: BoxDecoration(
        color: enabled ? primary.withValues(alpha: 0.04) : Colors.transparent,
        border: Border.all(
          color: over
              ? AppColors.error
              : enabled
                  ? primary.withValues(alpha: 0.35)
                  : border,
        ),
        borderRadius: BorderRadius.circular(14),
      ),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          InkWell(
            onTap: () => setState(() {
              if (enabled) {
                _enabledSectors.remove(sector.sectorId);
              } else {
                _enabledSectors.add(sector.sectorId);
              }
            }),
            child: Row(
              children: [
                Container(
                  width: 20,
                  height: 20,
                  decoration: BoxDecoration(
                    color: enabled ? primary : Colors.transparent,
                    border: Border.all(color: enabled ? primary : border, width: 2),
                    borderRadius: BorderRadius.circular(6),
                  ),
                  child: Icon(enabled ? LucideIcons.check : LucideIcons.minus,
                      size: 13, color: enabled ? Colors.white : textTertiary),
                ),
                const SizedBox(width: 10),
                Expanded(
                  child: Column(
                    crossAxisAlignment: CrossAxisAlignment.start,
                    children: [
                      Text(sector.name,
                          style: const TextStyle(fontSize: 14, fontWeight: FontWeight.w700),
                          overflow: TextOverflow.ellipsis),
                      Text('Kapacitet ${sector.capacity} · preostalo ${sector.remaining}',
                          style: TextStyle(fontSize: 11, color: textTertiary),
                          overflow: TextOverflow.ellipsis),
                    ],
                  ),
                ),
                const SizedBox(width: 8),
                Container(
                  padding: const EdgeInsets.symmetric(horizontal: 9, vertical: 4),
                  decoration: BoxDecoration(
                    color: over
                        ? AppColors.error.withValues(alpha: 0.12)
                        : primary.withValues(alpha: 0.1),
                    borderRadius: BorderRadius.circular(20),
                  ),
                  child: Text(
                    enabled ? '$total kom' : 'isključeno',
                    style: TextStyle(
                      fontSize: 11,
                      fontWeight: FontWeight.w700,
                      color: over ? AppColors.error : primary,
                    ),
                  ),
                ),
              ],
            ),
          ),
          if (enabled) ...[
            const SizedBox(height: 12),
            Divider(color: border, height: 1),
            const SizedBox(height: 12),
            if (sector.ticketTypes.isEmpty)
              _buildQuantityRow(sector, null, 'Redovna', sector.price)
            else
              for (final type in sector.ticketTypes) ...[
                _buildQuantityRow(sector, type.id, type.name, type.price),
                if (type != sector.ticketTypes.last) const SizedBox(height: 10),
              ],
            const SizedBox(height: 10),
            Wrap(
              spacing: 6,
              runSpacing: 6,
              children: [
                for (final preset in _presets)
                  OutlinedButton(
                    onPressed: () => _setQuantity(
                      sector.sectorId,
                      sector.ticketTypes.isEmpty ? null : sector.ticketTypes.first.id,
                      preset,
                    ),
                    style: OutlinedButton.styleFrom(
                      padding: const EdgeInsets.symmetric(horizontal: 11, vertical: 5),
                      minimumSize: Size.zero,
                      tapTargetSize: MaterialTapTargetSize.shrinkWrap,
                      shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(20)),
                    ),
                    child: Text(
                      '$preset × ${sector.ticketTypes.isEmpty ? 'Redovna' : sector.ticketTypes.first.name}',
                      style: const TextStyle(fontSize: 11, fontWeight: FontWeight.w600),
                    ),
                  ),
              ],
            ),
          ],
        ],
      ),
    );
  }

  Widget _buildQuantityRow(
      TicketPrintSectorOption sector, String? ticketTypeId, String name, double price) {
    final isDark = _isDark;
    final textTertiary = isDark ? AppColors.darkTextTertiary : AppColors.lightTextTertiary;
    final border = isDark ? AppColors.darkBorderInput : AppColors.lightBorderInput;
    final quantity = _quantityOf(sector.sectorId, ticketTypeId);

    return Row(
      children: [
        Expanded(
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              Text(name,
                  style: const TextStyle(fontSize: 13, fontWeight: FontWeight.w600),
                  overflow: TextOverflow.ellipsis),
              Text(_formatMoney(price), style: TextStyle(fontSize: 11, color: textTertiary)),
            ],
          ),
        ),
        const SizedBox(width: 10),
        Container(
          decoration: BoxDecoration(
            border: Border.all(color: border),
            borderRadius: BorderRadius.circular(10),
          ),
          child: Row(
            mainAxisSize: MainAxisSize.min,
            children: [
              _StepperButton(
                icon: LucideIcons.minus,
                onTap: quantity <= 0 ? null : () => _setQuantity(sector.sectorId, ticketTypeId, quantity - 10),
              ),
              SizedBox(
                width: 46,
                child: Text('$quantity',
                    textAlign: TextAlign.center,
                    style: const TextStyle(fontSize: 13, fontWeight: FontWeight.w700)),
              ),
              _StepperButton(
                icon: LucideIcons.plus,
                onTap: quantity >= sector.remaining
                    ? null
                    : () => _setQuantity(sector.sectorId, ticketTypeId, quantity + 10),
              ),
            ],
          ),
        ),
      ],
    );
  }

  // --------------------------------------------------------- preview column

  Widget _buildPreviewColumn() {
    final isDark = _isDark;
    final textTertiary = isDark ? AppColors.darkTextTertiary : AppColors.lightTextTertiary;
    final previews = _buildPreviewModels();

    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        _InfoBanner(
          icon: LucideIcons.info,
          color: AppColors.info,
          text: _totalTickets > 0
              ? 'Pregled prvih ${previews.length} od $_totalTickets karata. Svaka karta dobija jedinstveni broj i QR kod za validaciju na ulazu.'
              : 'Odaberite sektor i broj karata da vidite pregled.',
        ),
        const SizedBox(height: 16),
        if (previews.isNotEmpty)
          Wrap(
            spacing: 16,
            runSpacing: 16,
            children: [for (final preview in previews) _TicketPreviewCard(model: preview)],
          ),
        if (_totalTickets > previews.length) ...[
          const SizedBox(height: 14),
          Container(
            width: double.infinity,
            padding: const EdgeInsets.symmetric(horizontal: 16, vertical: 14),
            decoration: BoxDecoration(
              border: Border.all(
                  color: isDark ? AppColors.darkBorderInput : AppColors.lightBorderInput,
                  style: BorderStyle.solid),
              borderRadius: BorderRadius.circular(12),
            ),
            child: Text(
              'Još ${_totalTickets - previews.length} karata biće generisano u PDF-u istim rasporedom.',
              style: TextStyle(fontSize: 13, color: textTertiary),
            ),
          ),
        ],
        const SizedBox(height: 20),
        ConstrainedBox(
          constraints: const BoxConstraints(maxWidth: 640),
          child: Text(
            'Ulaznica ne podliježe povratu ili zamjeni. Organizator zadržava pravo izmjene programa. '
            'Ponovni ulazak nije dozvoljen. Predočiti QR kod na ulazu.',
            style: TextStyle(fontSize: 11, height: 1.5, color: textTertiary),
          ),
        ),
      ],
    );
  }

  /// The first handful of tickets the batch would produce, in the same order the
  /// renderer numbers them.
  List<_PreviewModel> _buildPreviewModels() {
    final options = _options;
    if (options == null) return const [];

    final models = <_PreviewModel>[];
    var serial = options.nextSerialNumber;

    for (final sector in options.sectors) {
      if (!_enabledSectors.contains(sector.sectorId)) continue;

      final lines = sector.ticketTypes.isEmpty
          ? [(name: 'Redovna', price: sector.price, id: null as String?)]
          : [for (final t in sector.ticketTypes) (name: t.name, price: t.price, id: t.id as String?)];

      for (final line in lines) {
        final quantity = _quantityOf(sector.sectorId, line.id);
        for (var i = 0; i < quantity; i++) {
          if (models.length < 6) {
            models.add(_PreviewModel(
              sectorName: sector.name,
              typeName: line.name,
              price: line.price,
              serial: serial,
              productName: widget.product.name,
            ));
          }
          serial++;
        }
      }
    }

    return models;
  }

  static String _formatMoney(double amount) => '${amount.toStringAsFixed(2).replaceAll('.', ',')} KM';
}

// ------------------------------------------------------------------ widgets

class _StepperButton extends StatelessWidget {
  final IconData icon;
  final VoidCallback? onTap;

  const _StepperButton({required this.icon, this.onTap});

  @override
  Widget build(BuildContext context) {
    final isDark = Theme.of(context).brightness == Brightness.dark;
    return InkWell(
      onTap: onTap,
      child: Container(
        width: 30,
        height: 32,
        alignment: Alignment.center,
        color: isDark ? AppColors.darkSurfaceMuted : AppColors.lightSurfaceMuted,
        child: Icon(icon,
            size: 14,
            color: onTap == null
                ? (isDark ? AppColors.darkTextDisabled : AppColors.lightTextDisabled)
                : (isDark ? AppColors.darkTextSecondary : AppColors.lightTextSecondary)),
      ),
    );
  }
}

class _SummaryRow extends StatelessWidget {
  final String label;
  final String value;
  final Color? valueColor;

  const _SummaryRow({required this.label, required this.value, this.valueColor});

  @override
  Widget build(BuildContext context) {
    final isDark = Theme.of(context).brightness == Brightness.dark;
    final textTertiary = isDark ? AppColors.darkTextTertiary : AppColors.lightTextTertiary;

    return Row(
      children: [
        Expanded(child: Text(label, style: TextStyle(fontSize: 13, color: textTertiary))),
        const SizedBox(width: 8),
        Text(value, style: TextStyle(fontSize: 13, fontWeight: FontWeight.w700, color: valueColor)),
      ],
    );
  }
}

class _InfoBanner extends StatelessWidget {
  final IconData icon;
  final Color color;
  final String text;

  const _InfoBanner({required this.icon, required this.color, required this.text});

  @override
  Widget build(BuildContext context) {
    final isDark = Theme.of(context).brightness == Brightness.dark;

    return Container(
      padding: const EdgeInsets.symmetric(horizontal: 14, vertical: 12),
      decoration: BoxDecoration(
        color: isDark ? AppColors.darkSurface : Colors.white,
        border: Border.all(color: color.withValues(alpha: 0.35)),
        borderRadius: BorderRadius.circular(12),
      ),
      child: Row(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Icon(icon, size: 16, color: color),
          const SizedBox(width: 10),
          Expanded(child: Text(text, style: const TextStyle(fontSize: 13))),
        ],
      ),
    );
  }
}

class _PreviewModel {
  final String sectorName;
  final String typeName;
  final double price;
  final int serial;
  final String productName;

  const _PreviewModel({
    required this.sectorName,
    required this.typeName,
    required this.price,
    required this.serial,
    required this.productName,
  });
}

/// A scale mock-up of one printed ticket. The QR is a placeholder pattern on
/// purpose: these tickets do not exist yet, so there is no signed payload to
/// render — the real code is minted server-side when the batch is created.
class _TicketPreviewCard extends StatelessWidget {
  final _PreviewModel model;

  const _TicketPreviewCard({required this.model});

  @override
  Widget build(BuildContext context) {
    final isDark = Theme.of(context).brightness == Brightness.dark;

    return Container(
      width: 420,
      height: 170,
      decoration: BoxDecoration(
        color: isDark ? AppColors.darkSurface : Colors.white,
        border: Border.all(color: isDark ? AppColors.darkBorder : AppColors.lightBorder),
        borderRadius: BorderRadius.circular(16),
      ),
      clipBehavior: Clip.antiAlias,
      child: Row(
        children: [
          Expanded(
            child: Container(
              padding: const EdgeInsets.all(18),
              decoration: const BoxDecoration(
                gradient: LinearGradient(
                  begin: Alignment.topLeft,
                  end: Alignment.bottomRight,
                  colors: [AppColors.primary, AppColors.primaryDark],
                ),
              ),
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                mainAxisAlignment: MainAxisAlignment.spaceBetween,
                children: [
                  const Text('ULAZNICA · EKARTA',
                      style: TextStyle(
                          fontSize: 10, fontWeight: FontWeight.w600, color: AppColors.accent, letterSpacing: 1.2)),
                  Text(
                    model.productName,
                    maxLines: 2,
                    overflow: TextOverflow.ellipsis,
                    style: const TextStyle(fontSize: 15, fontWeight: FontWeight.w700, color: Colors.white),
                  ),
                  Row(
                    crossAxisAlignment: CrossAxisAlignment.end,
                    children: [
                      Expanded(
                        child: _PreviewField(label: 'SEKTOR', value: model.sectorName),
                      ),
                      const SizedBox(width: 10),
                      Expanded(
                        child: _PreviewField(
                          label: 'TIP / CIJENA',
                          value: '${model.typeName} · ${model.price.toStringAsFixed(2).replaceAll('.', ',')} KM',
                        ),
                      ),
                    ],
                  ),
                ],
              ),
            ),
          ),
          Container(
            width: 130,
            padding: const EdgeInsets.all(14),
            color: isDark ? AppColors.darkSurfaceSubtle : AppColors.lightSurfaceSubtle,
            child: Column(
              mainAxisAlignment: MainAxisAlignment.center,
              children: [
                Container(
                  width: 64,
                  height: 64,
                  decoration: BoxDecoration(
                    color: isDark ? AppColors.darkSurfaceMuted : AppColors.lightSurfaceMuted,
                    border: Border.all(color: isDark ? AppColors.darkBorder : AppColors.lightBorder),
                    borderRadius: BorderRadius.circular(8),
                  ),
                  child: Icon(LucideIcons.qrCode,
                      size: 32, color: isDark ? AppColors.darkTextDisabled : AppColors.lightTextDisabled),
                ),
                const SizedBox(height: 8),
                Text(
                  'KARTA #${model.serial.toString().padLeft(6, '0')}',
                  textAlign: TextAlign.center,
                  style: TextStyle(
                    fontSize: 9,
                    fontWeight: FontWeight.w600,
                    color: isDark ? AppColors.darkTextTertiary : AppColors.lightTextDisabled,
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

class _PreviewField extends StatelessWidget {
  final String label;
  final String value;

  const _PreviewField({required this.label, required this.value});

  @override
  Widget build(BuildContext context) {
    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        Text(label,
            style: TextStyle(
                fontSize: 9, letterSpacing: 0.5, color: Colors.white.withValues(alpha: 0.7))),
        Text(value,
            maxLines: 1,
            overflow: TextOverflow.ellipsis,
            style: const TextStyle(fontSize: 12, fontWeight: FontWeight.w700, color: Colors.white)),
      ],
    );
  }
}
