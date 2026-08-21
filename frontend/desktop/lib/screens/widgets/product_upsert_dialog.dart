import 'dart:async';

import 'package:flutter/material.dart';
import 'package:lucide_icons_flutter/lucide_icons.dart';

import '../../main.dart';
import '../../models/enums/city.dart';
import '../../models/enums/ticketing_mode.dart';
import '../../models/requests/product_upsert_request.dart';
import '../../models/responses/category_response.dart';
import '../../models/responses/product_preview_response.dart';
import '../../models/responses/product_response.dart';
import '../../models/search_objects/base_search_object.dart';
import '../../providers/category_provider.dart';
import '../../providers/product_provider.dart';
import '../../theme/app_colors.dart';
import '../../utility/snackbar_service.dart';
import '../../widgets/confirm_dialog.dart';
import 'product_location_picker.dart';

/// Create/edit dialog for Product — follows the backend's preview→create
/// pattern (see .claude/rules/01-domain.md): a brand-new product must be
/// previewed before it can be saved as a draft; editing an existing one
/// skips the preview step and saves directly. The Date field only shows
/// when the selected category's TicketingMode is SingleOccurrence — the
/// other two modes don't use a single product-level date at all.
class ProductUpsertDialog extends StatefulWidget {
  final ProductResponse? product;

  /// Called with the just-created/just-updated product — the response the backend already
  /// returned from insert()/update(), so callers can adopt it directly instead of re-querying
  /// getMine() (which only works if the edited product happens to land on page 1).
  final void Function(ProductResponse product) onSaved;

  const ProductUpsertDialog({super.key, this.product, required this.onSaved});

  @override
  State<ProductUpsertDialog> createState() => _ProductUpsertDialogState();
}

class _ProductUpsertDialogState extends State<ProductUpsertDialog> {
  final _formKey = GlobalKey<FormState>();
  final _nameCtrl = TextEditingController();
  final _descCtrl = TextEditingController();
  final _provider = ProductProvider();

  List<CategoryResponse> _categories = [];
  bool _isLoadingCategories = true;
  int? _selectedCategoryId;
  DateTime? _selectedDate;
  City? _selectedCity;
  double? _latitude;
  double? _longitude;

  bool _isSaving = false;
  bool _hasPreviewed = false;
  ProductPreviewResponse? _preview;
  StreamSubscription<void>? _categoryRefreshSubscription;

  bool get _isEditing => widget.product != null;
  bool get _isDark => Theme.of(context).brightness == Brightness.dark;
  Color get _primary => _isDark ? AppColors.secondary : AppColors.primary;
  Color get _primaryDark => _isDark ? AppColors.primary : AppColors.primaryDark;

  CategoryResponse? get _selectedCategory {
    if (_selectedCategoryId == null) return null;
    for (final c in _categories) {
      if (c.id == _selectedCategoryId) return c;
    }
    return null;
  }

  bool get _needsDate => _selectedCategory?.ticketingMode == TicketingMode.singleOccurrence;

  @override
  void initState() {
    super.initState();
    if (_isEditing) {
      final p = widget.product!;
      _nameCtrl.text = p.name;
      _descCtrl.text = p.description;
      _selectedCategoryId = p.categoryId;
      _selectedDate = p.date;
      _selectedCity = p.city;
      _latitude = p.latitude;
      _longitude = p.longitude;
    }
    _loadCategories();
    // Refetch whenever a category is created/edited/deleted anywhere else in the app — see
    // CategoryProvider.categoryRefreshBus.
    _categoryRefreshSubscription = CategoryProvider.categoryRefreshBus.stream.listen((_) => _loadCategories());
  }

  @override
  void dispose() {
    _categoryRefreshSubscription?.cancel();
    _nameCtrl.dispose();
    _descCtrl.dispose();
    super.dispose();
  }

  Future<void> _loadCategories() async {
    try {
      // Same "page through the server's 100-item ceiling" pattern as
      // CategoryMultiSelectFilter — see its comment for why.
      final provider = CategoryProvider();
      final all = <CategoryResponse>[];
      var page = 0;
      const pageSize = 100;
      while (true) {
        final result = await provider.getAll(
          searchObject: BaseSearchObject(page: page, pageSize: pageSize),
          fromJson: CategoryResponse.fromJson,
        );
        all.addAll(result.items);
        if (result.items.isEmpty || all.length >= result.totalCount) break;
        page++;
      }
      if (mounted) {
        setState(() {
          _categories = all;
          _isLoadingCategories = false;
        });
      }
    } catch (e) {
      if (mounted) {
        setState(() => _isLoadingCategories = false);
        handleApiError(e);
      }
    }
  }

  Future<void> _pickDate() async {
    final now = DateTime.now();
    final date = await showDatePicker(
      context: context,
      initialDate: _selectedDate ?? now.add(const Duration(days: 1)),
      firstDate: now,
      lastDate: DateTime(now.year + 5),
    );
    if (date == null || !mounted) return;

    final time = await showTimePicker(
      context: context,
      initialTime: _selectedDate != null
          ? TimeOfDay.fromDateTime(_selectedDate!)
          : const TimeOfDay(hour: 20, minute: 0),
    );
    if (time == null || !mounted) return;

    final combined = DateTime(date.year, date.month, date.day, time.hour, time.minute);
    // The backend rejects Date <= DateTime.UtcNow — picking today's calendar day still allows a
    // past time-of-day, so catch that here instead of only after a round trip to the API.
    if (combined.isBefore(DateTime.now())) {
      SnackbarService.showError('Odabrani datum i vrijeme moraju biti u budućnosti.');
      return;
    }

    setState(() {
      _selectedDate = combined;
      _hasPreviewed = false;
    });
  }

  ProductUpsertRequest _buildRequest() => ProductUpsertRequest(
        name: _nameCtrl.text.trim(),
        description: _descCtrl.text.trim(),
        date: _needsDate ? _selectedDate : null,
        categoryId: _selectedCategoryId!,
        latitude: _latitude,
        longitude: _longitude,
        city: _selectedCity,
      );

  /// Mirrors the backend's CreateProductRequestValidator: Latitude/Longitude/City are required,
  /// same tier as Name/Category — the organizer must place an exact pin, not just pick a city.
  bool _validateLocation() {
    if (_latitude == null || _longitude == null) {
      SnackbarService.showError('Lokacija je obavezna — postavite tačku na mapi.');
      return false;
    }
    if (_selectedCity == null) {
      SnackbarService.showError('Grad je obavezan.');
      return false;
    }
    return true;
  }

  Future<void> _doPreview() async {
    if (!_formKey.currentState!.validate()) return;
    if (_selectedCategoryId == null) {
      SnackbarService.showError('Kategorija je obavezna.');
      return;
    }
    if (_needsDate && _selectedDate == null) {
      SnackbarService.showError('Datum je obavezan za ovaj tip kategorije.');
      return;
    }
    if (!_validateLocation()) return;

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
      // Create always goes through the preview step first — see class doc.
      if (!_hasPreviewed) {
        await _doPreview();
        return;
      }
    } else {
      if (!_formKey.currentState!.validate()) return;
      if (_selectedCategoryId == null) {
        SnackbarService.showError('Kategorija je obavezna.');
        return;
      }
      if (_needsDate && _selectedDate == null) {
        SnackbarService.showError('Datum je obavezan za ovaj tip kategorije.');
        return;
      }
      if (!_validateLocation()) return;
    }

    final confirmed = await ConfirmDialog.show(
      context,
      title: _isEditing ? 'Spremi izmjene' : 'Sačuvaj proizvod',
      message: _isEditing
          ? 'Da li ste sigurni da želite sačuvati izmjene proizvoda "${_nameCtrl.text.trim()}"?'
          : 'Da li ste sigurni da želite sačuvati proizvod "${_nameCtrl.text.trim()}" kao nacrt?',
      confirmLabel: 'Spremi',
      destructive: false,
    );
    if (confirmed != true || !mounted) return;

    setState(() => _isSaving = true);
    try {
      final saved = _isEditing
          ? await _provider.updateProduct(widget.product!.id, _buildRequest())
          : await _provider.createProduct(_buildRequest());
      if (mounted) {
        Navigator.of(context).pop();
        widget.onSaved(saved);
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
    final onPrimaryColor = isDark ? AppColors.darkBackground : Colors.white;

    return Dialog(
      backgroundColor: Colors.transparent,
      insetPadding: const EdgeInsets.symmetric(horizontal: 32, vertical: 24),
      child: ConstrainedBox(
        constraints: const BoxConstraints(maxWidth: 560),
        child: ClipRRect(
          borderRadius: BorderRadius.circular(18),
          child: Material(
            color: isDark ? AppColors.darkSurface : Colors.white,
            child: Column(
              mainAxisSize: MainAxisSize.min,
              children: [
                // ── Header ──
                Container(
                  padding: const EdgeInsets.symmetric(horizontal: 24, vertical: 18),
                  decoration: BoxDecoration(gradient: LinearGradient(colors: [_primary, _primaryDark])),
                  child: Row(
                    children: [
                      Expanded(
                        child: Text(
                          _isEditing ? 'Uredi Proizvod' : 'Kreiraj Novi Proizvod',
                          style: TextStyle(fontSize: 20, fontWeight: FontWeight.w700, color: onPrimaryColor),
                        ),
                      ),
                      InkWell(
                        borderRadius: BorderRadius.circular(8),
                        onTap: () => Navigator.of(context).pop(),
                        hoverColor: Colors.white12,
                        child: Padding(
                          padding: const EdgeInsets.all(6),
                          child: Icon(LucideIcons.x, color: onPrimaryColor, size: 20),
                        ),
                      ),
                    ],
                  ),
                ),

                // ── Scrollable form ──
                Flexible(
                  child: SingleChildScrollView(
                    padding: const EdgeInsets.all(24),
                    child: Form(
                      key: _formKey,
                      child: Column(
                        crossAxisAlignment: CrossAxisAlignment.start,
                        children: [
                          _FieldLabel('Naziv *'),
                          const SizedBox(height: 6),
                          TextFormField(
                            controller: _nameCtrl,
                            decoration: _inputDecoration('npr. Ljetni Muzički Festival'),
                            style: TextStyle(fontSize: 14, color: textPrimary),
                            onChanged: (_) => _resetPreview(),
                            validator: (v) {
                              if (v == null || v.trim().isEmpty) return 'Naziv je obavezan';
                              if (v.trim().length < 2 || v.trim().length > 200) {
                                return 'Naziv mora biti između 2 i 200 karaktera';
                              }
                              return null;
                            },
                          ),
                          const SizedBox(height: 14),
                          _FieldLabel('Opis'),
                          const SizedBox(height: 6),
                          TextFormField(
                            controller: _descCtrl,
                            maxLines: 3,
                            decoration: _inputDecoration('Kratak opis proizvoda...'),
                            style: TextStyle(fontSize: 14, color: textPrimary),
                            onChanged: (_) => _resetPreview(),
                            validator: (v) {
                              if (v != null && v.trim().length > 2000) {
                                return 'Opis može imati maksimalno 2000 karaktera';
                              }
                              return null;
                            },
                          ),
                          const SizedBox(height: 14),
                          _FieldLabel('Kategorija *'),
                          const SizedBox(height: 6),
                          _isLoadingCategories
                              ? const Padding(
                                  padding: EdgeInsets.symmetric(vertical: 12),
                                  child: SizedBox(
                                      width: 20, height: 20, child: CircularProgressIndicator(strokeWidth: 2)),
                                )
                              : DropdownButtonFormField<int>(
                                  initialValue: _selectedCategoryId,
                                  decoration: _inputDecoration('Odaberite kategoriju'),
                                  style: TextStyle(fontSize: 14, color: textPrimary),
                                  items: _categories
                                      .map((c) => DropdownMenuItem(value: c.id, child: Text(c.name)))
                                      .toList(),
                                  onChanged: (id) => setState(() {
                                    _selectedCategoryId = id;
                                    _resetPreview();
                                  }),
                                  validator: (v) => v == null ? 'Kategorija je obavezna' : null,
                                ),
                          if (_selectedCategory != null) ...[
                            const SizedBox(height: 6),
                            Text(
                              _selectedCategory!.ticketingMode.description,
                              style: TextStyle(
                                  fontSize: 12,
                                  color: isDark ? AppColors.darkTextTertiary : AppColors.lightTextTertiary),
                            ),
                          ],
                          const SizedBox(height: 14),
                          _FieldLabel('Grad *'),
                          const SizedBox(height: 6),
                          DropdownButtonFormField<City>(
                            initialValue: _selectedCity,
                            decoration: _inputDecoration('Odaberite grad'),
                            style: TextStyle(fontSize: 14, color: textPrimary),
                            items: City.values.map((c) => DropdownMenuItem(value: c, child: Text(c.label))).toList(),
                            onChanged: (city) => setState(() {
                              _selectedCity = city;
                              _resetPreview();
                            }),
                            validator: (v) => v == null ? 'Grad je obavezan' : null,
                          ),
                          const SizedBox(height: 14),
                          _FieldLabel('Lokacija (tačka na mapi) *'),
                          const SizedBox(height: 6),
                          ProductLocationPicker(
                            initialLatitude: _latitude,
                            initialLongitude: _longitude,
                            onLocationChanged: (lat, lng) => setState(() {
                              _latitude = lat;
                              _longitude = lng;
                              _resetPreview();
                            }),
                          ),
                          if (_needsDate) ...[
                            const SizedBox(height: 14),
                            _FieldLabel('Datum i Vrijeme *'),
                            const SizedBox(height: 6),
                            InkWell(
                              borderRadius: BorderRadius.circular(12),
                              onTap: _pickDate,
                              child: InputDecorator(
                                decoration: _inputDecoration('Odaberite datum i vrijeme', prefixIcon: LucideIcons.calendarDays),
                                child: Text(
                                  _selectedDate != null ? _formatDateTime(_selectedDate!) : 'Nije odabrano',
                                  style: TextStyle(
                                      fontSize: 14,
                                      color: _selectedDate != null
                                          ? textPrimary
                                          : (isDark ? AppColors.darkTextDisabled : AppColors.lightTextDisabled)),
                                ),
                              ),
                            ),
                          ],
                          if (_isEditing) ...[
                            const SizedBox(height: 10),
                            Text(
                              'Slike proizvoda se dodaju i uklanjaju na stranici proizvoda, nakon spremanja.',
                              style: TextStyle(
                                  fontSize: 12,
                                  fontStyle: FontStyle.italic,
                                  color: isDark ? AppColors.darkTextTertiary : AppColors.lightTextTertiary),
                            ),
                          ],
                          if (_hasPreviewed && _preview != null) ...[
                            const SizedBox(height: 20),
                            _PreviewCard(preview: _preview!, isDark: isDark),
                          ],
                        ],
                      ),
                    ),
                  ),
                ),

                // ── Footer ──
                Container(
                  padding: const EdgeInsets.symmetric(horizontal: 24, vertical: 16),
                  decoration: BoxDecoration(
                    color: isDark ? AppColors.darkSurfaceSubtle : AppColors.lightSurfaceSubtle,
                    border: Border(top: BorderSide(color: isDark ? AppColors.darkBorder : AppColors.lightBorder)),
                  ),
                  child: Row(
                    mainAxisAlignment: MainAxisAlignment.end,
                    children: [
                      OutlinedButton(
                        onPressed: _isSaving ? null : () => Navigator.of(context).pop(),
                        style: OutlinedButton.styleFrom(
                          foregroundColor: isDark ? AppColors.darkTextSecondary : AppColors.lightTextSecondary,
                          side: BorderSide(color: isDark ? AppColors.darkBorderInput : AppColors.lightBorderInput),
                          shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(12)),
                          padding: const EdgeInsets.symmetric(horizontal: 20, vertical: 12),
                        ),
                        child: const Text('Otkaži', style: TextStyle(fontWeight: FontWeight.w500, fontSize: 14)),
                      ),
                      const SizedBox(width: 10),
                      if (!_isEditing && !_hasPreviewed) ...[
                        Container(
                          decoration: BoxDecoration(
                            gradient: LinearGradient(colors: [_primary, _primaryDark]),
                            borderRadius: BorderRadius.circular(12),
                          ),
                          child: Material(
                            color: Colors.transparent,
                            child: InkWell(
                              borderRadius: BorderRadius.circular(12),
                              onTap: _isSaving ? null : _doPreview,
                              child: Padding(
                                padding: const EdgeInsets.symmetric(horizontal: 20, vertical: 12),
                                child: _isSaving
                                    ? SizedBox(
                                        width: 16,
                                        height: 16,
                                        child: CircularProgressIndicator(strokeWidth: 2, color: onPrimaryColor))
                                    : Text('Pregled',
                                        style: TextStyle(color: onPrimaryColor, fontWeight: FontWeight.w600, fontSize: 14)),
                              ),
                            ),
                          ),
                        ),
                      ] else ...[
                        if (!_isEditing)
                          Padding(
                            padding: const EdgeInsets.only(right: 10),
                            child: OutlinedButton(
                              onPressed: _isSaving ? null : _resetPreview,
                              style: OutlinedButton.styleFrom(
                                foregroundColor: isDark ? AppColors.darkTextSecondary : AppColors.lightTextSecondary,
                                side: BorderSide(color: isDark ? AppColors.darkBorderInput : AppColors.lightBorderInput),
                                shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(12)),
                                padding: const EdgeInsets.symmetric(horizontal: 20, vertical: 12),
                              ),
                              child: const Text('Nazad', style: TextStyle(fontWeight: FontWeight.w500, fontSize: 14)),
                            ),
                          ),
                        Container(
                          decoration: BoxDecoration(
                            gradient: LinearGradient(colors: [_primary, _primaryDark]),
                            borderRadius: BorderRadius.circular(12),
                          ),
                          child: Material(
                            color: Colors.transparent,
                            child: InkWell(
                              borderRadius: BorderRadius.circular(12),
                              onTap: _isSaving ? null : _doSave,
                              child: Padding(
                                padding: const EdgeInsets.symmetric(horizontal: 20, vertical: 12),
                                child: _isSaving
                                    ? SizedBox(
                                        width: 16,
                                        height: 16,
                                        child: CircularProgressIndicator(strokeWidth: 2, color: onPrimaryColor))
                                    : Text(_isEditing ? 'Spremi Izmjene' : 'Sačuvaj kao Nacrt',
                                        style: TextStyle(color: onPrimaryColor, fontWeight: FontWeight.w600, fontSize: 14)),
                              ),
                            ),
                          ),
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

String _formatDateTime(DateTime date) {
  final d = date.toLocal();
  const months = ['', 'jan', 'feb', 'mar', 'apr', 'maj', 'jun', 'jul', 'aug', 'sep', 'okt', 'nov', 'dec'];
  final hh = d.hour.toString().padLeft(2, '0');
  final mm = d.minute.toString().padLeft(2, '0');
  return '${d.day}. ${months[d.month]} ${d.year}. u $hh:$mm';
}

class _FieldLabel extends StatelessWidget {
  final String text;
  const _FieldLabel(this.text);

  @override
  Widget build(BuildContext context) {
    final isDark = Theme.of(context).brightness == Brightness.dark;
    return Text(
      text,
      style: TextStyle(
        fontSize: 13,
        fontWeight: FontWeight.w600,
        color: isDark ? AppColors.darkTextSecondary : AppColors.lightTextSecondary,
      ),
    );
  }
}

class _PreviewCard extends StatelessWidget {
  final ProductPreviewResponse preview;
  final bool isDark;

  const _PreviewCard({required this.preview, required this.isDark});

  @override
  Widget build(BuildContext context) {
    final primary = isDark ? AppColors.secondary : AppColors.primary;
    final textPrimary = isDark ? AppColors.darkTextPrimary : AppColors.lightTextPrimary;
    final textTertiary = isDark ? AppColors.darkTextTertiary : AppColors.lightTextTertiary;
    return Container(
      padding: const EdgeInsets.all(16),
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
              Icon(LucideIcons.eye, size: 16, color: primary),
              const SizedBox(width: 8),
              Text('Pregled', style: TextStyle(fontSize: 13, fontWeight: FontWeight.w700, color: primary)),
            ],
          ),
          const SizedBox(height: 10),
          Text(preview.name, style: TextStyle(fontSize: 16, fontWeight: FontWeight.w700, color: textPrimary)),
          if (preview.description.isNotEmpty) ...[
            const SizedBox(height: 4),
            Text(preview.description, style: TextStyle(fontSize: 13, color: textTertiary)),
          ],
          const SizedBox(height: 8),
          Wrap(
            spacing: 12,
            runSpacing: 4,
            children: [
              Text('Kategorija: ${preview.categoryName}', style: TextStyle(fontSize: 12, color: textTertiary)),
              if (preview.date != null)
                Text('Datum: ${_formatDateTime(preview.date!)}', style: TextStyle(fontSize: 12, color: textTertiary)),
              Text('Vrsta: ${preview.ticketingMode.label}', style: TextStyle(fontSize: 12, color: textTertiary)),
              Text('Grad: ${preview.city.label}', style: TextStyle(fontSize: 12, color: textTertiary)),
            ],
          ),
        ],
      ),
    );
  }
}
