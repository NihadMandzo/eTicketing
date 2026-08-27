import 'dart:async';
import 'dart:math' as math;

import 'package:flutter/material.dart';
import 'package:lucide_icons_flutter/lucide_icons.dart';

import '../core/export_notifications.dart';
import '../core/formatting.dart';
import '../main.dart';
import '../models/enums/ticketing_mode.dart';
import '../models/requests/create_ticket_print_batch_request.dart';
import '../models/responses/product_response.dart';
import '../models/responses/ticket_print_batch_response.dart';
import '../models/responses/ticket_print_options_response.dart';
import '../providers/ticket_print_provider.dart';
import '../theme/app_colors.dart';
import 'widgets/quantity_stepper_field.dart';
import 'widgets/ticket_export_download.dart';
import 'widgets/ticket_sheet_preview.dart';

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
///
/// <b>Layout.</b> Two columns that both fill the viewport: a fixed input rail on
/// the left, and on the right the outcome — a run readout above a
/// true-proportion A4 sheet showing the first three tickets exactly as they will
/// print, cut rules and all. The sheet is the point of this screen: what an
/// organizer needs before committing paper is not a list of numbers but
/// confidence about what comes out of the printer.
class TicketExportScreen extends StatefulWidget {
  final ProductResponse product;

  const TicketExportScreen({super.key, required this.product});

  @override
  State<TicketExportScreen> createState() => _TicketExportScreenState();
}

class _TicketExportScreenState extends State<TicketExportScreen> {
  static const _presets = [25, 50, 100, 250];
  static const _pollInterval = Duration(seconds: 2);

  /// Below this the two columns stack — the rail plus a legible sheet needs
  /// roughly this much before the sheet shrinks into uselessness.
  static const _twoColumnBreakpoint = 1080.0;

  static const _railWidth = 372.0;

  final _provider = TicketPrintProvider();

  TicketPrintOptionsResponse? _options;
  bool _isLoading = true;
  bool _loadFailed = false;

  /// Which sectors are switched on, and how many tickets of each
  /// `sectorId|ticketTypeId` line the organizer wants.
  final Set<String> _enabledSectors = {};
  final Map<String, int> _quantities = {};

  /// One text controller per quantity line. They live here rather than inside
  /// the row widget because the presets, the reset button and a finished
  /// download all have to push values back into a field the organizer may also
  /// be typing into.
  final Map<String, TextEditingController> _quantityFields = {};

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
    for (final field in _quantityFields.values) {
      field.dispose();
    }
    super.dispose();
  }

  // ------------------------------------------------------------------ loading

  Future<void> _load() async {
    setState(() {
      _isLoading = true;
      _loadFailed = false;
    });

    try {
      final options = await _provider.getOptions(
        widget.product.id,
        date: _validDate,
      );
      // Picks up a render started before the organizer navigated away, so
      // coming back to this product shows the batch instead of a blank form.
      final latest = await _provider.getLatestForProduct(widget.product.id);

      if (!mounted) return;
      setState(() {
        _options = options;
        _isLoading = false;
        if (latest != null &&
            (latest.status.isInFlight || latest.isDownloadable)) {
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
        _loadFailed = true;
      });
      handleApiError(e);
    }
  }

  /// Re-reads remaining capacity without discarding the current selection —
  /// used after picking a DailyEntry date (capacity is per calendar day),
  /// right after a batch is created (its hold already claimed capacity), and
  /// again once that batch finishes rendering, since a batch that fails still
  /// keeps the tickets it minted and the capacity they used.
  ///
  /// Refreshing alone would leave stale numbers sitting in the quantity
  /// fields even though the sector cards now show less room — [_setQuantity]
  /// already clamps on every keystroke, but nothing re-runs that clamp
  /// against figures the organizer typed *before* the numbers moved. This
  /// reconciles every currently-entered line against the new capacity so nothing
  /// in the form is left describing tickets that no longer fit.
  Future<void> _refreshOptions() async {
    try {
      final options = await _provider.getOptions(
        widget.product.id,
        date: _validDate,
      );
      if (!mounted) return;
      setState(() {
        _options = options;
        _reconcileQuantitiesWithCapacity(options);
      });
    } catch (e) {
      handleApiError(e);
    }
  }

  /// Clamps every non-zero quantity line down to its sector's freshly-read
  /// `remaining`, rewriting the visible field to match. `_validationError` /
  /// `_capacityError` are reactive getters over `_options`/`_quantities`, so
  /// the rest of the screen (the readout, the capacity banner, the export
  /// button) re-validates for free the moment this changes them.
  void _reconcileQuantitiesWithCapacity(TicketPrintOptionsResponse options) {
    for (final sector in options.sectors) {
      final lines = sector.ticketTypes.isEmpty
          ? [null as String?]
          : [for (final type in sector.ticketTypes) type.id as String?];

      for (final ticketTypeId in lines) {
        final current = _quantityOf(sector.sectorId, ticketTypeId);
        if (current == 0 || current <= sector.remaining) continue;

        _quantities[_lineKey(sector.sectorId, ticketTypeId)] =
            sector.remaining;
        _writeField(sector.sectorId, ticketTypeId, sector.remaining);
      }
    }
  }

  // ---------------------------------------------------------------- selection

  String _lineKey(String sectorId, String? ticketTypeId) =>
      '$sectorId|${ticketTypeId ?? ''}';

  int _quantityOf(String sectorId, String? ticketTypeId) =>
      _quantities[_lineKey(sectorId, ticketTypeId)] ?? 0;

  /// Total requested for one sector across all its price tiers.
  int _sectorTotal(TicketPrintSectorOption sector) {
    if (!_enabledSectors.contains(sector.sectorId)) return 0;
    if (sector.ticketTypes.isEmpty) return _quantityOf(sector.sectorId, null);
    return sector.ticketTypes.fold(
      0,
      (sum, t) => sum + _quantityOf(sector.sectorId, t.id),
    );
  }

  bool _isOverCapacity(TicketPrintSectorOption sector) =>
      _sectorTotal(sector) > sector.remaining;

  List<TicketPrintSectorOption> get _sectors => _options?.sectors ?? const [];

  int get _totalTickets => _sectors.fold(0, (sum, s) => sum + _sectorTotal(s));

  double get _totalValue {
    var total = 0.0;
    for (final sector in _sectors) {
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

  int get _ticketsPerSheet => _options?.ticketsPerSheet ?? 3;

  int get _totalSheets => (_totalTickets / _ticketsPerSheet).ceil();

  /// The one blocking rule the user asked for by name: never more tickets than
  /// the sector has room for. Mirrors the backend's `print.capacity_exceeded`,
  /// which re-checks atomically regardless.
  String? get _capacityError {
    for (final sector in _sectors) {
      if (_isOverCapacity(sector)) {
        return 'Sektor "${sector.name}" ima još ${formatCount(sector.remaining)} slobodnih mjesta.';
      }
    }
    return null;
  }

  String? get _validationError {
    final options = _options;
    if (options == null) return null;
    if (!options.canExport) return options.blockedReason;
    if (options.ticketingMode == TicketingMode.dailyEntry &&
        _validDate == null) {
      return 'Odaberite datum za koji ulaznice važe.';
    }
    if (_totalTickets == 0) return null;
    if (_totalTickets > options.maxTicketsPerBatch) {
      return 'Jedan izvoz može sadržavati najviše ${formatCount(options.maxTicketsPerBatch)} ulaznica.';
    }
    return _capacityError;
  }

  bool get _canSubmit =>
      _totalTickets > 0 &&
      _validationError == null &&
      !_isSubmitting &&
      (_options?.canExport ?? false);

  void _setQuantity(
    String sectorId,
    String? ticketTypeId,
    int value, {
    bool writeField = true,
  }) {
    final sector = _sectors.where((s) => s.sectorId == sectorId).firstOrNull;
    // Clamped at the sector's remaining capacity so neither the stepper nor a
    // typed figure can walk past what the backend will accept.
    final ceiling = sector?.remaining ?? 0;
    final clamped = value.clamp(0, ceiling);

    setState(() => _quantities[_lineKey(sectorId, ticketTypeId)] = clamped);
    if (writeField) _writeField(sectorId, ticketTypeId, clamped);
  }

  /// The controller for one line, created on first use with whatever the line
  /// already holds. Zero shows as an empty field with a `0` hint rather than a
  /// literal `0`, so typing a figure never has to start by deleting one.
  TextEditingController _fieldFor(String sectorId, String? ticketTypeId) {
    final key = _lineKey(sectorId, ticketTypeId);
    return _quantityFields.putIfAbsent(key, () {
      final quantity = _quantities[key] ?? 0;
      return TextEditingController(
        text: quantity == 0 ? '' : quantity.toString(),
      );
    });
  }

  void _writeField(String sectorId, String? ticketTypeId, int value) {
    final field = _fieldFor(sectorId, ticketTypeId);
    final text = value == 0 ? '' : value.toString();
    if (field.text == text) return;

    field.value = TextEditingValue(
      text: text,
      selection: TextSelection.collapsed(offset: text.length),
    );
  }

  /// A figure typed straight into the field. The field is only rewritten when
  /// the capacity clamp actually bit — otherwise typing "1" on the way to "150"
  /// would be fought with on every keystroke.
  void _onQuantityTyped(
    TicketPrintSectorOption sector,
    String? ticketTypeId,
    String raw,
  ) {
    final typed = int.tryParse(raw) ?? 0;
    final clamped = typed.clamp(0, sector.remaining);
    _setQuantity(
      sector.sectorId,
      ticketTypeId,
      clamped,
      writeField: clamped != typed,
    );
  }

  void _clearQuantities() {
    _quantities.clear();
    for (final field in _quantityFields.values) {
      field.clear();
    }
  }

  void _resetSelection() {
    setState(() {
      _clearQuantities();
      for (final sector in _sectors) {
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
        if (quantity > 0) {
          lines.add(
            TicketPrintLineRequest(
              sectorId: sector.sectorId,
              quantity: quantity,
            ),
          );
        }
      } else {
        for (final type in sector.ticketTypes) {
          final quantity = _quantityOf(sector.sectorId, type.id);
          if (quantity > 0) {
            lines.add(
              TicketPrintLineRequest(
                sectorId: sector.sectorId,
                ticketTypeId: type.id,
                quantity: quantity,
              ),
            );
          }
        }
      }
    }

    try {
      final batch = await _provider.create(
        CreateTicketPrintBatchRequest(
          productId: widget.product.id,
          validDate: _validDate,
          lines: lines,
        ),
      );

      if (!mounted) return;
      setState(() {
        _batch = batch;
        _isSubmitting = false;
      });

      handleApiSuccess(
        'Ulaznice su izdate. PDF se priprema — možete nastaviti s radom.',
      );
      exportNotifications.refresh();
      _startPolling();
      // The hold behind this batch already claimed capacity the moment it was
      // created, not when the render finishes — refresh now so "Slobodno …"
      // is never stale while the PDF is still being prepared.
      await _refreshOptions();
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
        // A batch that fails still keeps the tickets it already minted (and
        // the capacity they used) — Ready or Failed, the export is finished
        // and the numbers on screen must catch up to that.
        await _refreshOptions();
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
      _clearQuantities();
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
    final sector = _sectors.firstOrNull;
    final now = DateTime.now();
    final year = sector?.periodYear ?? now.year;
    final month = sector?.periodMonth ?? now.month;
    final first = DateTime(year, month, 1);
    final last = DateTime(year, month + 1, 0);
    final withinPeriod = now.isAfter(first) && now.isBefore(last);

    final picked = await showDatePicker(
      context: context,
      initialDate: _validDate ?? (withinPeriod ? now : first),
      firstDate: first,
      lastDate: last,
      helpText: 'Datum za koji ulaznice važe',
      cancelText: 'Odustani',
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
    final textPrimary = isDark
        ? AppColors.darkTextPrimary
        : AppColors.lightTextPrimary;
    final textTertiary = isDark
        ? AppColors.darkTextTertiary
        : AppColors.lightTextTertiary;

    return Scaffold(
      backgroundColor: isDark
          ? AppColors.darkBackground
          : AppColors.lightBackground,
      appBar: AppBar(
        backgroundColor: isDark ? AppColors.darkSurface : Colors.white,
        foregroundColor: textPrimary,
        elevation: 0,
        titleSpacing: 0,
        title: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          mainAxisAlignment: MainAxisAlignment.center,
          children: [
            const Text(
              'Izvoz fizičkih ulaznica',
              style: TextStyle(fontSize: 18, fontWeight: FontWeight.w700),
            ),
            Text(
              '${widget.product.name} — spremno za štampu na A4 papiru',
              style: TextStyle(
                fontSize: 12,
                fontWeight: FontWeight.w400,
                color: textTertiary,
              ),
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
          : _loadFailed
          ? _buildLoadError(textTertiary)
          : LayoutBuilder(
              builder: (context, constraints) {
                if (constraints.maxWidth >= _twoColumnBreakpoint) {
                  return Row(
                    crossAxisAlignment: CrossAxisAlignment.stretch,
                    children: [
                      SizedBox(width: _railWidth, child: _buildRail()),
                      Expanded(child: _buildStage()),
                    ],
                  );
                }

                // Stacked: the rail is the job, so it comes first. Both
                // halves get explicit heights — an Expanded would collapse
                // to nothing inside a scroll view.
                return SingleChildScrollView(
                  child: Column(
                    children: [
                      SizedBox(
                        height: math.max(constraints.maxHeight * 0.62, 460),
                        child: _buildRail(),
                      ),
                      SizedBox(
                        height: math.max(constraints.maxHeight, 680),
                        child: _buildStage(),
                      ),
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
          Text(
            'Podaci za izvoz nisu učitani.',
            style: TextStyle(color: textTertiary),
            textAlign: TextAlign.center,
          ),
          const SizedBox(height: 12),
          OutlinedButton(onPressed: _load, child: const Text('Pokušaj ponovo')),
        ],
      ),
    ),
  );

  // ------------------------------------------------------- the primary button

  /// The four states the export walks through: export → preparing → download →
  /// retry. Each label names what pressing it does.
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
                width: 16,
                height: 16,
                child: CircularProgressIndicator(
                  strokeWidth: 2,
                  color: Colors.white,
                ),
              )
            : const Icon(LucideIcons.download, size: 16),
        label: Text(
          _isDownloading
              ? 'Preuzimanje…'
              : 'Preuzmi PDF (${formatCount(batch.ticketCount)} karata)',
        ),
      );
    }

    if (batch != null && batch.status.isInFlight) {
      final progress = batch.ticketCount == 0
          ? ''
          : ' ${formatCount(batch.renderedCount)}/${formatCount(batch.ticketCount)}';
      return FilledButton.icon(
        onPressed: null,
        icon: const SizedBox(
          width: 16,
          height: 16,
          child: CircularProgressIndicator(strokeWidth: 2),
        ),
        label: Text('Priprema PDF-a…$progress'),
      );
    }

    return FilledButton.icon(
      onPressed: _canSubmit ? _submit : null,
      icon: _isSubmitting
          ? const SizedBox(
              width: 16,
              height: 16,
              child: CircularProgressIndicator(
                strokeWidth: 2,
                color: Colors.white,
              ),
            )
          : const Icon(LucideIcons.fileDown, size: 16),
      label: Text(
        _totalTickets > 0
            ? 'Izvezi ${formatCount(_totalTickets)} karata (PDF)'
            : 'Izvezi PDF',
      ),
    );
  }

  // ---------------------------------------------------------------- left rail

  Widget _buildRail() {
    final isDark = _isDark;
    final options = _options!;
    final border = isDark ? AppColors.darkBorder : AppColors.lightBorder;
    final textTertiary = isDark
        ? AppColors.darkTextTertiary
        : AppColors.lightTextTertiary;
    final error = _validationError;

    return Container(
      decoration: BoxDecoration(
        color: isDark ? AppColors.darkSurface : Colors.white,
        border: Border(right: BorderSide(color: border)),
      ),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: [
          Padding(
            padding: const EdgeInsets.fromLTRB(20, 20, 20, 14),
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                _Eyebrow(text: 'Odaberite ulaznice', color: textTertiary),
                const SizedBox(height: 6),
                Text(
                  'Označite sektore i unesite broj karata po tipu.',
                  style: TextStyle(
                    fontSize: 12,
                    height: 1.4,
                    color: textTertiary,
                  ),
                ),
                if (!options.canExport) ...[
                  const SizedBox(height: 14),
                  _Banner(
                    icon: LucideIcons.circleAlert,
                    color: AppColors.warningDark,
                    text:
                        options.blockedReason ??
                        'Izvoz nije moguć za ovaj proizvod.',
                  ),
                ],
                if (options.ticketingMode == TicketingMode.dailyEntry) ...[
                  const SizedBox(height: 14),
                  _buildDateRow(border, textTertiary),
                ],
              ],
            ),
          ),
          Expanded(
            child: options.sectors.isEmpty
                ? Center(
                    child: Padding(
                      padding: const EdgeInsets.symmetric(horizontal: 20),
                      child: Text(
                        'Ovaj proizvod još nema objavljenih sektora.',
                        textAlign: TextAlign.center,
                        style: TextStyle(fontSize: 13, color: textTertiary),
                      ),
                    ),
                  )
                : ListView.separated(
                    padding: const EdgeInsets.fromLTRB(20, 0, 20, 20),
                    itemCount: options.sectors.length,
                    separatorBuilder: (_, _) => const SizedBox(height: 12),
                    itemBuilder: (context, index) =>
                        _buildSectorCard(options.sectors[index]),
                  ),
          ),
          if (error != null)
            Padding(
              padding: const EdgeInsets.fromLTRB(20, 0, 20, 12),
              child: _Banner(
                icon: LucideIcons.triangleAlert,
                color: AppColors.error,
                text: error,
              ),
            ),
          Container(
            padding: const EdgeInsets.fromLTRB(20, 8, 12, 8),
            decoration: BoxDecoration(
              border: Border(top: BorderSide(color: border)),
            ),
            child: Row(
              children: [
                Expanded(
                  child: Text(
                    'Numeracija kreće od ${formatStubNumber(options.nextSerialNumber)}',
                    style: TextStyle(
                      fontSize: 12,
                      color: textTertiary,
                      fontFeatures: const [FontFeature.tabularFigures()],
                    ),
                    overflow: TextOverflow.ellipsis,
                  ),
                ),
                const SizedBox(width: 8),
                TextButton(
                  onPressed: _totalTickets == 0 ? null : _resetSelection,
                  style: TextButton.styleFrom(
                    foregroundColor: textTertiary,
                    padding: const EdgeInsets.symmetric(
                      horizontal: 12,
                      vertical: 10,
                    ),
                    minimumSize: Size.zero,
                    tapTargetSize: MaterialTapTargetSize.shrinkWrap,
                  ),
                  child: const Text(
                    'Poništi izbor',
                    style: TextStyle(fontSize: 12, fontWeight: FontWeight.w600),
                  ),
                ),
              ],
            ),
          ),
        ],
      ),
    );
  }

  Widget _buildDateRow(Color border, Color textTertiary) {
    final label = _validDate == null
        ? 'Odaberite datum'
        : formatDate(_validDate!);

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
            Icon(LucideIcons.calendarDays, size: 15, color: textTertiary),
            const SizedBox(width: 10),
            Expanded(
              child: Text(
                'Ulaznice važe za: $label',
                style: const TextStyle(
                  fontSize: 13,
                  fontWeight: FontWeight.w600,
                ),
                overflow: TextOverflow.ellipsis,
              ),
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
    final textTertiary = isDark
        ? AppColors.darkTextTertiary
        : AppColors.lightTextTertiary;
    final accent = over ? AppColors.error : primary;

    return Container(
      decoration: BoxDecoration(
        color: enabled ? accent.withValues(alpha: 0.05) : Colors.transparent,
        border: Border.all(
          color: enabled ? accent.withValues(alpha: over ? 0.9 : 0.3) : border,
        ),
        borderRadius: BorderRadius.circular(12),
      ),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: [
          InkWell(
            borderRadius: const BorderRadius.vertical(top: Radius.circular(11)),
            onTap: () => setState(() {
              if (enabled) {
                _enabledSectors.remove(sector.sectorId);
              } else {
                _enabledSectors.add(sector.sectorId);
              }
            }),
            child: Padding(
              padding: const EdgeInsets.fromLTRB(12, 12, 12, 10),
              child: Row(
                children: [
                  Container(
                    width: 18,
                    height: 18,
                    decoration: BoxDecoration(
                      color: enabled ? accent : Colors.transparent,
                      border: Border.all(
                        color: enabled ? accent : border,
                        width: 1.6,
                      ),
                      borderRadius: BorderRadius.circular(5),
                    ),
                    child: enabled
                        ? const Icon(
                            LucideIcons.check,
                            size: 12,
                            color: Colors.white,
                          )
                        : null,
                  ),
                  const SizedBox(width: 10),
                  Expanded(
                    child: Column(
                      crossAxisAlignment: CrossAxisAlignment.start,
                      children: [
                        Text(
                          sector.name,
                          style: const TextStyle(
                            fontSize: 14,
                            fontWeight: FontWeight.w700,
                          ),
                          overflow: TextOverflow.ellipsis,
                        ),
                        const SizedBox(height: 1),
                        Text(
                          'Slobodno ${formatCount(sector.remaining)} od ${formatCount(sector.capacity)}',
                          style: TextStyle(
                            fontSize: 11,
                            color: textTertiary,
                            fontFeatures: const [FontFeature.tabularFigures()],
                          ),
                          overflow: TextOverflow.ellipsis,
                        ),
                      ],
                    ),
                  ),
                  if (enabled && total > 0) ...[
                    const SizedBox(width: 8),
                    Text(
                      formatCount(total),
                      style: TextStyle(
                        fontSize: 15,
                        fontWeight: FontWeight.w700,
                        color: accent,
                        fontFeatures: const [FontFeature.tabularFigures()],
                      ),
                    ),
                  ],
                ],
              ),
            ),
          ),
          if (enabled) ...[
            Divider(color: border, height: 1),
            Padding(
              padding: const EdgeInsets.fromLTRB(12, 10, 12, 12),
              child: Column(
                children: [
                  if (sector.ticketTypes.isEmpty)
                    _buildQuantityRow(sector, null, 'Redovna', sector.price)
                  else
                    for (final type in sector.ticketTypes) ...[
                      _buildQuantityRow(sector, type.id, type.name, type.price),
                      if (type != sector.ticketTypes.last)
                        const SizedBox(height: 8),
                    ],
                  const SizedBox(height: 10),
                  Row(
                    children: [
                      for (final preset in _presets) ...[
                        Expanded(
                          child: _PresetChip(
                            label: formatCount(preset),
                            enabled: preset <= sector.remaining,
                            onTap: () => _setQuantity(
                              sector.sectorId,
                              sector.ticketTypes.isEmpty
                                  ? null
                                  : sector.ticketTypes.first.id,
                              preset,
                            ),
                          ),
                        ),
                        if (preset != _presets.last) const SizedBox(width: 6),
                      ],
                    ],
                  ),
                ],
              ),
            ),
          ],
        ],
      ),
    );
  }

  Widget _buildQuantityRow(
    TicketPrintSectorOption sector,
    String? ticketTypeId,
    String name,
    double price,
  ) {
    final isDark = _isDark;
    final textTertiary = isDark
        ? AppColors.darkTextTertiary
        : AppColors.lightTextTertiary;
    final quantity = _quantityOf(sector.sectorId, ticketTypeId);

    return Row(
      children: [
        Expanded(
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              Text(
                name,
                style: const TextStyle(
                  fontSize: 13,
                  fontWeight: FontWeight.w600,
                ),
                overflow: TextOverflow.ellipsis,
              ),
              Text(
                formatMoney(price),
                style: TextStyle(fontSize: 11, color: textTertiary),
              ),
            ],
          ),
        ),
        const SizedBox(width: 10),
        // Typed as well as stepped: ±10 is for nudging, but a box office
        // printing 1.250 tickets should not have to press a button 125 times.
        // Capacity still clamps whatever is typed.
        QuantityStepperField(
          controller: _fieldFor(sector.sectorId, ticketTypeId),
          onChanged: (raw) => _onQuantityTyped(sector, ticketTypeId, raw),
          onDecrement: quantity <= 0
              ? null
              : () =>
                    _setQuantity(sector.sectorId, ticketTypeId, quantity - 10),
          onIncrement: quantity >= sector.remaining
              ? null
              : () =>
                    _setQuantity(sector.sectorId, ticketTypeId, quantity + 10),
        ),
      ],
    );
  }

  // -------------------------------------------------------------- right stage

  Widget _buildStage() {
    final isDark = _isDark;
    final textTertiary = isDark
        ? AppColors.darkTextTertiary
        : AppColors.lightTextTertiary;
    final previews = _previewTickets();

    return Padding(
      padding: const EdgeInsets.fromLTRB(24, 20, 24, 20),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: [
          _buildReadout(),
          const SizedBox(height: 18),
          Expanded(
            child: Center(
              child: AspectRatio(
                aspectRatio: TicketSheetPreview.a4Ratio,
                child: TicketSheetPreview(
                  sheet: _sheetContext(),
                  tickets: previews,
                  ticketsPerSheet: _ticketsPerSheet,
                ),
              ),
            ),
          ),
          const SizedBox(height: 14),
          Text(
            _sheetCaption(previews),
            textAlign: TextAlign.center,
            style: TextStyle(
              fontSize: 12,
              color: textTertiary,
              fontFeatures: const [FontFeature.tabularFigures()],
            ),
          ),
        ],
      ),
    );
  }

  /// Everything the sheet shows that belongs to the run rather than to one
  /// ticket — the same values `TicketPrintRenderer` hands to `PrintSheetModel`.
  PrintSheetContext _sheetContext() => PrintSheetContext(
    productName: widget.product.name,
    city: widget.product.city.label,
    productDate: widget.product.date,
    ticketingMode: _options?.ticketingMode ?? widget.product.ticketingMode,
    validDate: _validDate,
    issuedAt: DateTime.now(),
  );

  String _sheetCaption(List<PrintPreviewTicket> previews) {
    if (previews.isEmpty) {
      return 'Odaberite sektor i broj karata da vidite kako će list izgledati.';
    }

    final first = formatStubNumber(previews.first.serial);
    final last = formatStubNumber(previews.last.serial);
    return 'List 1 od ${formatCount(_totalSheets)} · ulaznice $first – $last · režite po isprekidanim linijama';
  }

  /// Count, sheets and face value — the three things an organizer signs off on
  /// before committing paper. One readout strip rather than three cards: this is
  /// a single fact about one print run, not three unrelated metrics.
  Widget _buildReadout() {
    final isDark = _isDark;
    final border = isDark ? AppColors.darkBorder : AppColors.lightBorder;

    return Container(
      padding: const EdgeInsets.symmetric(horizontal: 20, vertical: 14),
      decoration: BoxDecoration(
        color: isDark ? AppColors.darkSurface : Colors.white,
        border: Border.all(color: border),
        borderRadius: BorderRadius.circular(12),
      ),
      child: Row(
        children: [
          Expanded(
            child: _Metric(
              label: 'Ukupno karata',
              value: formatCount(_totalTickets),
            ),
          ),
          _MetricDivider(color: border),
          Expanded(
            child: _Metric(
              label: 'A4 listova · $_ticketsPerSheet po strani',
              value: formatCount(_totalSheets),
            ),
          ),
          _MetricDivider(color: border),
          Expanded(
            child: _Metric(
              label: 'Nominalna vrijednost',
              value: formatMoney(_totalValue),
              color: AppColors.successDark,
            ),
          ),
        ],
      ),
    );
  }

  /// The first sheet's worth of tickets, numbered exactly as the renderer will
  /// number them.
  List<PrintPreviewTicket> _previewTickets() {
    final options = _options;
    if (options == null) return const [];

    final tickets = <PrintPreviewTicket>[];
    var serial = options.nextSerialNumber;

    for (final sector in options.sectors) {
      if (!_enabledSectors.contains(sector.sectorId)) continue;

      // A sector with no ticket types prints its sector name alone, exactly as
      // the renderer does when `TicketTypeName` is null — "Redovna" is the
      // rail's label for that line, not something that reaches the paper.
      final lines = sector.ticketTypes.isEmpty
          ? [(name: null as String?, price: sector.price, id: null as String?)]
          : [
              for (final t in sector.ticketTypes)
                (name: t.name as String?, price: t.price, id: t.id as String?),
            ];

      for (final line in lines) {
        for (var i = 0; i < _quantityOf(sector.sectorId, line.id); i++) {
          if (tickets.length < _ticketsPerSheet) {
            tickets.add(
              PrintPreviewTicket(
                sector: sector.name,
                type: line.name,
                price: line.price,
                serial: serial,
              ),
            );
          }
          serial++;
        }
      }
    }

    return tickets;
  }
}

// --------------------------------------------------------------- rail pieces

class _Eyebrow extends StatelessWidget {
  final String text;
  final Color color;

  const _Eyebrow({required this.text, required this.color});

  @override
  Widget build(BuildContext context) => Text(
    text.toUpperCase(),
    style: TextStyle(
      fontSize: 11,
      fontWeight: FontWeight.w700,
      letterSpacing: 1.2,
      color: color,
    ),
  );
}

class _PresetChip extends StatelessWidget {
  final String label;
  final bool enabled;
  final VoidCallback onTap;

  const _PresetChip({
    required this.label,
    required this.enabled,
    required this.onTap,
  });

  @override
  Widget build(BuildContext context) {
    final isDark = Theme.of(context).brightness == Brightness.dark;
    final border = isDark
        ? AppColors.darkBorderInput
        : AppColors.lightBorderInput;
    final disabled = isDark
        ? AppColors.darkTextDisabled
        : AppColors.lightTextDisabled;

    return InkWell(
      borderRadius: BorderRadius.circular(7),
      onTap: enabled ? onTap : null,
      child: Container(
        height: 28,
        alignment: Alignment.center,
        decoration: BoxDecoration(
          border: Border.all(
            color: enabled ? border : border.withValues(alpha: 0.4),
          ),
          borderRadius: BorderRadius.circular(7),
        ),
        child: Text(
          label,
          style: TextStyle(
            fontSize: 11,
            fontWeight: FontWeight.w700,
            color: enabled ? null : disabled,
            fontFeatures: const [FontFeature.tabularFigures()],
          ),
        ),
      ),
    );
  }
}

class _Banner extends StatelessWidget {
  final IconData icon;
  final Color color;
  final String text;

  const _Banner({required this.icon, required this.color, required this.text});

  @override
  Widget build(BuildContext context) => Container(
    padding: const EdgeInsets.symmetric(horizontal: 12, vertical: 10),
    decoration: BoxDecoration(
      color: color.withValues(alpha: 0.08),
      border: Border.all(color: color.withValues(alpha: 0.35)),
      borderRadius: BorderRadius.circular(10),
    ),
    child: Row(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        Icon(icon, size: 15, color: color),
        const SizedBox(width: 9),
        Expanded(
          child: Text(text, style: const TextStyle(fontSize: 12, height: 1.35)),
        ),
      ],
    ),
  );
}

class _Metric extends StatelessWidget {
  final String label;
  final String value;
  final Color? color;

  const _Metric({required this.label, required this.value, this.color});

  @override
  Widget build(BuildContext context) {
    final isDark = Theme.of(context).brightness == Brightness.dark;
    final tertiary = isDark
        ? AppColors.darkTextTertiary
        : AppColors.lightTextTertiary;

    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        Text(
          label.toUpperCase(),
          style: TextStyle(
            fontSize: 10,
            fontWeight: FontWeight.w700,
            letterSpacing: 0.9,
            color: tertiary,
          ),
          overflow: TextOverflow.ellipsis,
        ),
        const SizedBox(height: 4),
        FittedBox(
          fit: BoxFit.scaleDown,
          alignment: Alignment.centerLeft,
          child: Text(
            value,
            style: TextStyle(
              fontSize: 24,
              fontWeight: FontWeight.w700,
              height: 1.1,
              color: color,
              fontFeatures: const [FontFeature.tabularFigures()],
            ),
          ),
        ),
      ],
    );
  }
}

class _MetricDivider extends StatelessWidget {
  final Color color;

  const _MetricDivider({required this.color});

  @override
  Widget build(BuildContext context) => Container(
    width: 1,
    height: 34,
    color: color,
    margin: const EdgeInsets.symmetric(horizontal: 18),
  );
}
