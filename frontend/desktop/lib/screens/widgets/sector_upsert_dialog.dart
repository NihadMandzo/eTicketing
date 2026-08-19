import 'package:flutter/material.dart';
import 'package:lucide_icons_flutter/lucide_icons.dart';

import '../../main.dart';
import '../../models/enums/ticketing_mode.dart';
import '../../models/requests/sector_upsert_request.dart';
import '../../models/responses/sector_preview_response.dart';
import '../../models/responses/sector_response.dart';
import '../../providers/sector_provider.dart';
import '../../theme/app_colors.dart';
import '../../utility/snackbar_service.dart';

/// Create/edit dialog for Sector — same preview→create pattern as
/// ProductUpsertDialog. Fields beyond Name/Capacity/Price are driven by the
/// parent Product's Category.TicketingMode (see .claude/rules/01-domain.md):
/// SingleOccurrence needs nothing extra; DailyEntry needs PeriodYear/
/// PeriodMonth; RecurringReservation locks Capacity to 1 (one specific
/// labeled space).
class SectorUpsertDialog extends StatefulWidget {
  final String productId;
  final TicketingMode ticketingMode;
  final SectorResponse? sector;
  final VoidCallback onSaved;

  const SectorUpsertDialog({
    super.key,
    required this.productId,
    required this.ticketingMode,
    this.sector,
    required this.onSaved,
  });

  @override
  State<SectorUpsertDialog> createState() => _SectorUpsertDialogState();
}

class _SectorUpsertDialogState extends State<SectorUpsertDialog> {
  final _formKey = GlobalKey<FormState>();
  final _nameCtrl = TextEditingController();
  final _capacityCtrl = TextEditingController();
  final _priceCtrl = TextEditingController();
  final _provider = SectorProvider();

  int? _periodYear;
  int? _periodMonth;

  bool _isSaving = false;
  bool _hasPreviewed = false;
  SectorPreviewResponse? _preview;

  bool get _isEditing => widget.sector != null;
  bool get _isDailyEntry => widget.ticketingMode == TicketingMode.dailyEntry;
  bool get _isRecurringReservation => widget.ticketingMode == TicketingMode.recurringReservation;
  bool get _isDark => Theme.of(context).brightness == Brightness.dark;
  Color get _primary => _isDark ? AppColors.secondary : AppColors.primary;
  Color get _primaryDark => _isDark ? AppColors.primary : AppColors.primaryDark;

  @override
  void initState() {
    super.initState();
    if (_isEditing) {
      final s = widget.sector!;
      _nameCtrl.text = s.name;
      _capacityCtrl.text = s.capacity.toString();
      _priceCtrl.text = s.price.toString();
      _periodYear = s.periodYear;
      _periodMonth = s.periodMonth;
    } else if (_isRecurringReservation) {
      _capacityCtrl.text = '1';
    }
  }

  @override
  void dispose() {
    _nameCtrl.dispose();
    _capacityCtrl.dispose();
    _priceCtrl.dispose();
    super.dispose();
  }

  SectorUpsertRequest _buildRequest() => SectorUpsertRequest(
        productId: widget.productId,
        name: _nameCtrl.text.trim(),
        capacity: int.parse(_capacityCtrl.text.trim()),
        price: double.parse(_priceCtrl.text.trim()),
        periodYear: _isDailyEntry ? _periodYear : null,
        periodMonth: _isDailyEntry ? _periodMonth : null,
      );

  bool _validateModeFields() {
    if (_isDailyEntry && (_periodYear == null || _periodMonth == null)) {
      SnackbarService.showError('Godina i mjesec su obavezni za ovaj tip proizvoda.');
      return false;
    }
    return true;
  }

  Future<void> _doPreview() async {
    if (!_formKey.currentState!.validate()) return;
    if (!_validateModeFields()) return;

    setState(() => _isSaving = true);
    try {
      final preview = await _provider.preview(_buildRequest());
      if (mounted) {
        setState(() {
          _preview = preview;
          _hasPreviewed = true;
          _isSaving = false;
        });
      }
    } catch (e) {
      if (mounted) {
        setState(() => _isSaving = false);
        handleApiError(e);
      }
    }
  }

  Future<void> _doSave() async {
    if (!_isEditing) {
      if (!_hasPreviewed) {
        await _doPreview();
        return;
      }
    } else {
      if (!_formKey.currentState!.validate()) return;
      if (!_validateModeFields()) return;
    }

    setState(() => _isSaving = true);
    try {
      if (_isEditing) {
        await _provider.updateSector(widget.sector!.id, _buildRequest());
      } else {
        await _provider.createSector(_buildRequest());
      }
      if (mounted) {
        Navigator.of(context).pop();
        widget.onSaved();
      }
    } catch (e) {
      if (mounted) {
        setState(() => _isSaving = false);
        handleApiError(e);
      }
    }
  }

  void _resetPreview() => setState(() {
        _hasPreviewed = false;
        _preview = null;
      });

  InputDecoration _inputDecoration(String hint, {IconData? prefixIcon}) {
    final placeholderColor = _isDark ? AppColors.darkTextTertiary : AppColors.lightTextDisabled;
    final borderColor = _isDark ? AppColors.darkBorderInput : AppColors.lightBorderInput;
    return InputDecoration(
      hintText: hint,
      hintStyle: TextStyle(color: placeholderColor, fontSize: 13),
      contentPadding: const EdgeInsets.symmetric(horizontal: 14, vertical: 12),
      prefixIcon: prefixIcon != null ? Icon(prefixIcon, color: placeholderColor, size: 18) : null,
      border: OutlineInputBorder(borderRadius: BorderRadius.circular(12), borderSide: BorderSide(color: borderColor)),
      enabledBorder:
          OutlineInputBorder(borderRadius: BorderRadius.circular(12), borderSide: BorderSide(color: borderColor)),
      focusedBorder: OutlineInputBorder(
          borderRadius: BorderRadius.circular(12), borderSide: BorderSide(color: _primary, width: 2)),
      errorBorder: OutlineInputBorder(
          borderRadius: BorderRadius.circular(12), borderSide: const BorderSide(color: AppColors.error)),
      focusedErrorBorder: OutlineInputBorder(
          borderRadius: BorderRadius.circular(12), borderSide: const BorderSide(color: AppColors.error, width: 2)),
      filled: true,
      fillColor: _isDark ? AppColors.darkInputFill : AppColors.lightInputFill,
    );
  }

  @override
  Widget build(BuildContext context) {
    final isDark = _isDark;
    final textPrimary = isDark ? AppColors.darkTextPrimary : AppColors.lightTextPrimary;
    final textTertiary = isDark ? AppColors.darkTextTertiary : AppColors.lightTextTertiary;
    final onPrimaryColor = isDark ? AppColors.darkBackground : Colors.white;

    return Dialog(
      backgroundColor: Colors.transparent,
      insetPadding: const EdgeInsets.symmetric(horizontal: 32, vertical: 40),
      child: ConstrainedBox(
        constraints: const BoxConstraints(maxWidth: 480),
        child: ClipRRect(
          borderRadius: BorderRadius.circular(18),
          child: Material(
            color: isDark ? AppColors.darkSurface : Colors.white,
            child: Column(
              mainAxisSize: MainAxisSize.min,
              children: [
                // ── Header ──
                Container(
                  padding: const EdgeInsets.symmetric(horizontal: 20, vertical: 16),
                  decoration: BoxDecoration(gradient: LinearGradient(colors: [_primary, _primaryDark])),
                  child: Row(
                    children: [
                      Expanded(
                        child: Text(
                          _isEditing ? 'Uredi Sektor' : 'Kreiraj Novi Sektor',
                          style: const TextStyle(fontSize: 18, fontWeight: FontWeight.w700, color: Colors.white),
                        ),
                      ),
                      InkWell(
                        borderRadius: BorderRadius.circular(8),
                        onTap: () => Navigator.of(context).pop(),
                        hoverColor: Colors.white12,
                        child: const Padding(
                          padding: EdgeInsets.all(6),
                          child: Icon(LucideIcons.x, color: Colors.white, size: 18),
                        ),
                      ),
                    ],
                  ),
                ),

                // ── Form ──
                Flexible(
                  child: SingleChildScrollView(
                    padding: const EdgeInsets.fromLTRB(20, 20, 20, 0),
                    child: Form(
                      key: _formKey,
                      child: Column(
                        crossAxisAlignment: CrossAxisAlignment.start,
                        mainAxisSize: MainAxisSize.min,
                        children: [
                          // Mode info banner
                          Container(
                            padding: const EdgeInsets.all(12),
                            decoration: BoxDecoration(
                              color: _primary.withValues(alpha: isDark ? 0.12 : 0.06),
                              borderRadius: BorderRadius.circular(10),
                              border: Border.all(color: _primary.withValues(alpha: 0.3)),
                            ),
                            child: Row(
                              crossAxisAlignment: CrossAxisAlignment.start,
                              children: [
                                Icon(LucideIcons.info, size: 16, color: _primary),
                                const SizedBox(width: 8),
                                Expanded(
                                  child: Text(
                                    '${widget.ticketingMode.label}: ${widget.ticketingMode.description}',
                                    style: TextStyle(fontSize: 12, color: textTertiary),
                                  ),
                                ),
                              ],
                            ),
                          ),
                          const SizedBox(height: 14),

                          _Label('Naziv *'),
                          const SizedBox(height: 6),
                          TextFormField(
                            controller: _nameCtrl,
                            decoration: _inputDecoration(
                                _isRecurringReservation ? 'npr. A-12' : 'npr. VIP, August 2026'),
                            style: TextStyle(fontSize: 14, color: textPrimary),
                            onChanged: (_) => _resetPreview(),
                            validator: (v) {
                              if (v == null || v.trim().isEmpty) return 'Naziv je obavezan';
                              if (v.trim().length > 200) return 'Naziv može imati maksimalno 200 karaktera';
                              return null;
                            },
                          ),
                          const SizedBox(height: 14),

                          Row(
                            crossAxisAlignment: CrossAxisAlignment.start,
                            children: [
                              Expanded(
                                child: Column(
                                  crossAxisAlignment: CrossAxisAlignment.start,
                                  children: [
                                    _Label('Kapacitet *'),
                                    const SizedBox(height: 6),
                                    TextFormField(
                                      controller: _capacityCtrl,
                                      enabled: !_isRecurringReservation,
                                      keyboardType: TextInputType.number,
                                      decoration: _inputDecoration('npr. 100'),
                                      style: TextStyle(fontSize: 14, color: textPrimary),
                                      onChanged: (_) => _resetPreview(),
                                      validator: (v) {
                                        final n = int.tryParse(v?.trim() ?? '');
                                        if (n == null || n <= 0) return 'Mora biti veće od 0';
                                        if (_isRecurringReservation && n != 1) {
                                          return 'Mora biti tačno 1 za ovaj tip';
                                        }
                                        return null;
                                      },
                                    ),
                                  ],
                                ),
                              ),
                              const SizedBox(width: 14),
                              Expanded(
                                child: Column(
                                  crossAxisAlignment: CrossAxisAlignment.start,
                                  children: [
                                    _Label('Cijena (KM) *'),
                                    const SizedBox(height: 6),
                                    TextFormField(
                                      controller: _priceCtrl,
                                      keyboardType: const TextInputType.numberWithOptions(decimal: true),
                                      decoration: _inputDecoration('npr. 50'),
                                      style: TextStyle(fontSize: 14, color: textPrimary),
                                      onChanged: (_) => _resetPreview(),
                                      validator: (v) {
                                        final n = double.tryParse(v?.trim() ?? '');
                                        if (n == null || n < 0) return 'Neispravna cijena';
                                        return null;
                                      },
                                    ),
                                  ],
                                ),
                              ),
                            ],
                          ),

                          if (_isDailyEntry) ...[
                            const SizedBox(height: 14),
                            Row(
                              crossAxisAlignment: CrossAxisAlignment.start,
                              children: [
                                Expanded(
                                  child: Column(
                                    crossAxisAlignment: CrossAxisAlignment.start,
                                    children: [
                                      _Label('Godina *'),
                                      const SizedBox(height: 6),
                                      DropdownButtonFormField<int>(
                                        initialValue: _periodYear,
                                        decoration: _inputDecoration('Godina'),
                                        style: TextStyle(fontSize: 14, color: textPrimary),
                                        items: List.generate(6, (i) => DateTime.now().year + i)
                                            .map((y) => DropdownMenuItem(value: y, child: Text('$y')))
                                            .toList(),
                                        onChanged: (y) => setState(() {
                                          _periodYear = y;
                                          _resetPreview();
                                        }),
                                      ),
                                    ],
                                  ),
                                ),
                                const SizedBox(width: 14),
                                Expanded(
                                  child: Column(
                                    crossAxisAlignment: CrossAxisAlignment.start,
                                    children: [
                                      _Label('Mjesec *'),
                                      const SizedBox(height: 6),
                                      DropdownButtonFormField<int>(
                                        initialValue: _periodMonth,
                                        decoration: _inputDecoration('Mjesec'),
                                        style: TextStyle(fontSize: 14, color: textPrimary),
                                        items: List.generate(12, (i) => i + 1)
                                            .map((m) => DropdownMenuItem(value: m, child: Text(_monthName(m))))
                                            .toList(),
                                        onChanged: (m) => setState(() {
                                          _periodMonth = m;
                                          _resetPreview();
                                        }),
                                      ),
                                    ],
                                  ),
                                ),
                              ],
                            ),
                            const SizedBox(height: 4),
                            Text(
                              'Kapacitet i cijena iznad se odnose na SVAKI dan u odabranom mjesecu.',
                              style: TextStyle(fontSize: 11, color: textTertiary),
                            ),
                          ],

                          if (_hasPreviewed && _preview != null) ...[
                            const SizedBox(height: 16),
                            _PreviewCard(preview: _preview!, isDark: isDark),
                          ],
                          const SizedBox(height: 4),
                        ],
                      ),
                    ),
                  ),
                ),

                // ── Footer ──
                Padding(
                  padding: const EdgeInsets.fromLTRB(20, 16, 20, 20),
                  child: Row(
                    mainAxisAlignment: MainAxisAlignment.end,
                    children: [
                      OutlinedButton(
                        onPressed: _isSaving ? null : () => Navigator.of(context).pop(),
                        style: OutlinedButton.styleFrom(
                          foregroundColor: isDark ? AppColors.darkTextSecondary : AppColors.lightTextSecondary,
                          side: BorderSide(color: isDark ? AppColors.darkBorderInput : AppColors.lightBorderInput),
                          shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(10)),
                          padding: const EdgeInsets.symmetric(horizontal: 18, vertical: 10),
                        ),
                        child: const Text('Otkaži', style: TextStyle(fontWeight: FontWeight.w500, fontSize: 13)),
                      ),
                      const SizedBox(width: 10),
                      if (!_isEditing && !_hasPreviewed)
                        FilledButton(
                          onPressed: _isSaving ? null : _doPreview,
                          style: FilledButton.styleFrom(
                            backgroundColor: _primary,
                            shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(10)),
                            padding: const EdgeInsets.symmetric(horizontal: 20, vertical: 10),
                            elevation: 0,
                          ),
                          child: _isSaving
                              ? SizedBox(
                                  width: 16, height: 16, child: CircularProgressIndicator(strokeWidth: 2, color: onPrimaryColor))
                              : Text('Pregled', style: TextStyle(fontWeight: FontWeight.w600, fontSize: 13, color: onPrimaryColor)),
                        )
                      else ...[
                        if (!_isEditing)
                          Padding(
                            padding: const EdgeInsets.only(right: 10),
                            child: OutlinedButton(
                              onPressed: _isSaving ? null : _resetPreview,
                              style: OutlinedButton.styleFrom(
                                foregroundColor: isDark ? AppColors.darkTextSecondary : AppColors.lightTextSecondary,
                                side: BorderSide(color: isDark ? AppColors.darkBorderInput : AppColors.lightBorderInput),
                                shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(10)),
                                padding: const EdgeInsets.symmetric(horizontal: 18, vertical: 10),
                              ),
                              child: const Text('Nazad', style: TextStyle(fontWeight: FontWeight.w500, fontSize: 13)),
                            ),
                          ),
                        FilledButton(
                          onPressed: _isSaving ? null : _doSave,
                          style: FilledButton.styleFrom(
                            backgroundColor: _primary,
                            shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(10)),
                            padding: const EdgeInsets.symmetric(horizontal: 20, vertical: 10),
                            elevation: 0,
                          ),
                          child: _isSaving
                              ? SizedBox(
                                  width: 16, height: 16, child: CircularProgressIndicator(strokeWidth: 2, color: onPrimaryColor))
                              : Text(_isEditing ? 'Spremi Izmjene' : 'Sačuvaj kao Nacrt',
                                  style: TextStyle(fontWeight: FontWeight.w600, fontSize: 13, color: onPrimaryColor)),
                        ),
                      ],
                    ],
                  ),
                ),
              ],
            ),
          ),
        ),
      ),
    );
  }
}

String _monthName(int m) {
  const months = ['', 'Januar', 'Februar', 'Mart', 'April', 'Maj', 'Juni', 'Juli', 'Avgust', 'Septembar', 'Oktobar', 'Novembar', 'Decembar'];
  return months[m];
}

class _Label extends StatelessWidget {
  final String text;
  const _Label(this.text);

  @override
  Widget build(BuildContext context) {
    final isDark = Theme.of(context).brightness == Brightness.dark;
    return Text(
      text,
      style: TextStyle(
        fontSize: 12,
        fontWeight: FontWeight.w600,
        color: isDark ? AppColors.darkTextSecondary : AppColors.lightTextSecondary,
      ),
    );
  }
}

class _PreviewCard extends StatelessWidget {
  final SectorPreviewResponse preview;
  final bool isDark;

  const _PreviewCard({required this.preview, required this.isDark});

  @override
  Widget build(BuildContext context) {
    final primary = isDark ? AppColors.secondary : AppColors.primary;
    final textPrimary = isDark ? AppColors.darkTextPrimary : AppColors.lightTextPrimary;
    final textTertiary = isDark ? AppColors.darkTextTertiary : AppColors.lightTextTertiary;
    return Container(
      padding: const EdgeInsets.all(14),
      decoration: BoxDecoration(
        color: primary.withValues(alpha: isDark ? 0.12 : 0.06),
        borderRadius: BorderRadius.circular(12),
        border: Border.all(color: primary.withValues(alpha: 0.3)),
      ),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Row(
            children: [
              Icon(LucideIcons.eye, size: 15, color: primary),
              const SizedBox(width: 8),
              Text('Pregled', style: TextStyle(fontSize: 12, fontWeight: FontWeight.w700, color: primary)),
            ],
          ),
          const SizedBox(height: 8),
          Text(preview.name, style: TextStyle(fontSize: 14, fontWeight: FontWeight.w700, color: textPrimary)),
          const SizedBox(height: 4),
          Wrap(
            spacing: 12,
            runSpacing: 4,
            children: [
              Text('Kapacitet: ${preview.capacity}', style: TextStyle(fontSize: 12, color: textTertiary)),
              Text('Cijena: ${preview.price.toStringAsFixed(2)} KM', style: TextStyle(fontSize: 12, color: textTertiary)),
              if (preview.periodYear != null && preview.periodMonth != null)
                Text('Period: ${_monthName(preview.periodMonth!)} ${preview.periodYear}',
                    style: TextStyle(fontSize: 12, color: textTertiary)),
            ],
          ),
        ],
      ),
    );
  }
}
