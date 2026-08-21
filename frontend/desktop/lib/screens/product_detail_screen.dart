import 'dart:io';
import 'dart:typed_data';

import 'package:file_picker/file_picker.dart';
import 'package:flutter/material.dart';
import 'package:lucide_icons_flutter/lucide_icons.dart';

import '../models/responses/product_image_response.dart';
import '../models/responses/product_response.dart';
import '../models/responses/sector_response.dart';
import '../models/search_objects/sector_search_object.dart';
import '../providers/product_provider.dart';
import '../providers/sector_provider.dart';
import '../theme/app_colors.dart';
import '../utility/image_validation.dart';
import '../utility/snackbar_service.dart';
import '../widgets/confirm_dialog.dart';
import 'widgets/product_location_picker.dart';
import 'widgets/product_upsert_dialog.dart';
import 'widgets/sector_upsert_dialog.dart';
import '../main.dart';

/// Client-side mirror of the backend's ProductImageValidation.MaxBytes/MaxCount — the backend
/// validator/service check is still authoritative regardless (see 00-workflow-and-testing.md).
const int _maxImageBytes = 2 * 1024 * 1024;
const int _maxImageCount = 5;

/// Organizer-facing product detail: header (name/description/date/status,
/// edit/publish/delete actions) + the product's Sectors, each with its own
/// edit/publish/delete — see .claude/rules/01-domain.md for the preview→
/// publish pattern this mirrors. Reached from ProductsScreen's "Pregled"
/// action, pushed via Navigator (no named-route table in this app, same
/// convention as OrganizationDetailScreen).
class ProductDetailScreen extends StatefulWidget {
  final ProductResponse product;

  const ProductDetailScreen({super.key, required this.product});

  @override
  State<ProductDetailScreen> createState() => _ProductDetailScreenState();
}

class _ProductDetailScreenState extends State<ProductDetailScreen> {
  late ProductResponse _product;
  final _sectorProvider = SectorProvider();

  List<SectorResponse> _sectors = [];
  bool _isLoadingSectors = true;
  bool _isPublishing = false;
  bool _isMutatingImage = false;

  bool get _isDark => Theme.of(context).brightness == Brightness.dark;

  @override
  void initState() {
    super.initState();
    _product = widget.product;
    _loadSectors();
  }

  Future<void> _loadSectors() async {
    setState(() => _isLoadingSectors = true);
    try {
      final result = await _sectorProvider.getMine(
        searchObject: SectorSearchObject(productId: _product.id, pageSize: 100),
      );
      if (mounted) {
        setState(() {
          _sectors = result.items;
          _isLoadingSectors = false;
        });
      }
    } catch (e) {
      if (mounted) {
        setState(() => _isLoadingSectors = false);
        handleApiError(e);
      }
    }
  }

  Future<void> _editProduct() async {
    await showDialog(
      context: context,
      barrierDismissible: false,
      builder: (_) => ProductUpsertDialog(
        product: _product,
        // updateProduct() already returns the fresh ProductResponse — adopt it directly instead
        // of re-querying getMine() (which only found the edit if it happened to land on page 1).
        onSaved: (updated) => setState(() => _product = updated),
      ),
    );
  }

  Future<void> _publishProduct() async {
    final confirmed = await ConfirmDialog.show(
      context,
      title: 'Objavi proizvod',
      message: 'Da li ste sigurni da želite objaviti proizvod "${_product.name}"? Postat će vidljiv svim kupcima.',
      confirmLabel: 'Objavi',
      destructive: false,
    );
    if (confirmed != true || !mounted) return;

    setState(() => _isPublishing = true);
    try {
      final updated = await ProductProvider().publish(_product.id);
      if (mounted) {
        setState(() {
          _product = updated;
          _isPublishing = false;
        });
        handleApiSuccess('Proizvod je objavljen.');
      }
    } catch (e) {
      if (mounted) {
        setState(() => _isPublishing = false);
        handleApiError(e);
      }
    }
  }

  Future<void> _addImage() async {
    if (_isMutatingImage || _product.images.length >= _maxImageCount) return;

    // PNG or JPEG, matching the backend validator (ProductImageValidation) — no forced square
    // crop here (unlike category icons/organization logos): product galleries render at varying
    // aspect ratios, so the raw picked file is uploaded as-is.
    final result = await FilePicker.platform.pickFiles(
      type: FileType.custom,
      allowedExtensions: ['png', 'jpg', 'jpeg'],
      allowMultiple: false,
    );
    if (result == null || result.files.single.path == null || !mounted) return;

    final path = result.files.single.path!;
    final bytes = await File(path).readAsBytes();
    if (!mounted) return;

    final error = ImageValidation.validateMaxBytes(
      bytes,
      maxBytes: _maxImageBytes,
      sizeErrorMessage: 'Slika može biti maksimalno 2MB.',
    );
    if (error != null) {
      SnackbarService.showError(error);
      return;
    }

    final isPng = path.toLowerCase().endsWith('.png');
    setState(() => _isMutatingImage = true);
    try {
      final updated = await ProductProvider().uploadImage(_product.id, Uint8List.fromList(bytes), isPng: isPng);
      if (mounted) {
        setState(() {
          _product = updated;
          _isMutatingImage = false;
        });
      }
    } catch (e) {
      if (mounted) {
        setState(() => _isMutatingImage = false);
        handleApiError(e);
      }
    }
  }

  Future<void> _deleteImage(ProductImageResponse image) async {
    if (_isMutatingImage) return;

    final confirmed = await ConfirmDialog.show(
      context,
      title: 'Obriši sliku',
      message: 'Da li ste sigurni da želite obrisati ovu sliku?',
      confirmLabel: 'Obriši',
    );
    if (confirmed != true || !mounted) return;

    setState(() => _isMutatingImage = true);
    try {
      final updated = await ProductProvider().deleteImage(_product.id, image.id);
      if (mounted) {
        setState(() {
          _product = updated;
          _isMutatingImage = false;
        });
      }
    } catch (e) {
      if (mounted) {
        setState(() => _isMutatingImage = false);
        handleApiError(e);
      }
    }
  }

  Future<void> _deleteProduct() async {
    final confirmed = await ConfirmDialog.show(
      context,
      title: 'Obriši proizvod',
      message: 'Da li ste sigurni da želite obrisati proizvod "${_product.name}"?',
      confirmLabel: 'Obriši',
    );
    if (confirmed != true || !mounted) return;

    try {
      await ProductProvider().delete(_product.id);
      if (mounted) Navigator.of(context).pop();
    } catch (e) {
      if (mounted) handleApiError(e);
    }
  }

  void _openSectorDialog({SectorResponse? sector}) {
    showDialog(
      context: context,
      barrierDismissible: false,
      builder: (_) => SectorUpsertDialog(
        productId: _product.id,
        ticketingMode: _product.ticketingMode,
        sector: sector,
        onSaved: _loadSectors,
      ),
    );
  }

  Future<void> _publishSector(SectorResponse sector) async {
    final confirmed = await ConfirmDialog.show(
      context,
      title: 'Objavi sektor',
      message: 'Da li ste sigurni da želite objaviti sektor "${sector.name}"? Postat će vidljiv svim kupcima.',
      confirmLabel: 'Objavi',
      destructive: false,
    );
    if (confirmed != true || !mounted) return;

    try {
      await _sectorProvider.publish(sector.id);
      if (mounted) {
        await _loadSectors();
        handleApiSuccess('Sektor je objavljen.');
      }
    } catch (e) {
      if (mounted) handleApiError(e);
    }
  }

  Future<void> _deleteSector(SectorResponse sector) async {
    final confirmed = await ConfirmDialog.show(
      context,
      title: 'Obriši sektor',
      message: 'Da li ste sigurni da želite obrisati sektor "${sector.name}"?',
      confirmLabel: 'Obriši',
    );
    if (confirmed != true || !mounted) return;

    try {
      await _sectorProvider.delete(sector.id);
      if (mounted) await _loadSectors();
    } catch (e) {
      if (mounted) handleApiError(e);
    }
  }

  @override
  Widget build(BuildContext context) {
    final isDark = _isDark;
    final primary = isDark ? AppColors.secondary : AppColors.primary;
    final textPrimary = isDark ? AppColors.darkTextPrimary : AppColors.lightTextPrimary;
    final textTertiary = isDark ? AppColors.darkTextTertiary : AppColors.lightTextTertiary;

    return Scaffold(
      backgroundColor: isDark ? AppColors.darkBackground : AppColors.lightBackground,
      appBar: AppBar(
        backgroundColor: isDark ? AppColors.darkSurface : Colors.white,
        foregroundColor: textPrimary,
        elevation: 0,
        title: Text(_product.name, style: const TextStyle(fontWeight: FontWeight.w700)),
        actions: [
          if (!_product.isPublished)
            TextButton.icon(
              onPressed: _isPublishing ? null : _publishProduct,
              icon: Icon(LucideIcons.send, size: 16, color: AppColors.successDark),
              label: Text('Objavi', style: TextStyle(color: AppColors.successDark, fontWeight: FontWeight.w600)),
            ),
          TextButton.icon(
            onPressed: _editProduct,
            icon: Icon(LucideIcons.pencil, size: 16, color: primary),
            label: Text('Uredi', style: TextStyle(color: primary, fontWeight: FontWeight.w600)),
          ),
          TextButton.icon(
            onPressed: _deleteProduct,
            icon: const Icon(LucideIcons.trash2, size: 16, color: AppColors.error),
            label: const Text('Obriši', style: TextStyle(color: AppColors.error, fontWeight: FontWeight.w600)),
          ),
          const SizedBox(width: 12),
        ],
      ),
      body: SingleChildScrollView(
        padding: const EdgeInsets.all(24),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            _buildHeader(isDark, textPrimary, textTertiary, primary),
            const SizedBox(height: 28),
            _buildImagesSection(isDark, textPrimary, textTertiary, primary),
            const SizedBox(height: 28),
            Row(
              children: [
                Text('Sektori', style: TextStyle(fontSize: 20, fontWeight: FontWeight.w700, color: textPrimary)),
                const Spacer(),
                Container(
                  decoration: BoxDecoration(
                    gradient: LinearGradient(colors: [primary, isDark ? AppColors.primary : AppColors.primaryDark]),
                    borderRadius: BorderRadius.circular(10),
                  ),
                  child: Material(
                    color: Colors.transparent,
                    child: InkWell(
                      borderRadius: BorderRadius.circular(10),
                      onTap: () => _openSectorDialog(),
                      child: const Padding(
                        padding: EdgeInsets.symmetric(horizontal: 14, vertical: 9),
                        child: Row(
                          children: [
                            Icon(LucideIcons.plus, color: Colors.white, size: 16),
                            SizedBox(width: 6),
                            Text('Dodaj Sektor', style: TextStyle(color: Colors.white, fontWeight: FontWeight.w600, fontSize: 13)),
                          ],
                        ),
                      ),
                    ),
                  ),
                ),
              ],
            ),
            const SizedBox(height: 16),
            if (_isLoadingSectors)
              SizedBox(height: 200, child: Center(child: CircularProgressIndicator(color: primary)))
            else if (_sectors.isEmpty)
              SizedBox(
                height: 160,
                child: Center(
                  child: Column(
                    mainAxisSize: MainAxisSize.min,
                    children: [
                      Icon(LucideIcons.layers, size: 40, color: isDark ? AppColors.darkBorderInput : AppColors.lightBorderInput),
                      const SizedBox(height: 10),
                      Text('Nema sektora', style: TextStyle(color: textTertiary, fontSize: 15)),
                    ],
                  ),
                ),
              )
            else
              ..._sectors.map((s) => _SectorTile(
                    sector: s,
                    isDark: isDark,
                    onEdit: () => _openSectorDialog(sector: s),
                    onPublish: () => _publishSector(s),
                    onDelete: () => _deleteSector(s),
                  )),
          ],
        ),
      ),
    );
  }

  Widget _buildHeader(bool isDark, Color textPrimary, Color textTertiary, Color primary) {
    final statusColor = _product.isPublished ? AppColors.success : AppColors.warning;
    final statusColorDark = _product.isPublished ? AppColors.successDark : AppColors.warningDark;

    return Container(
      padding: const EdgeInsets.all(20),
      decoration: BoxDecoration(
        color: isDark ? AppColors.darkSurface : Colors.white,
        border: Border.all(color: isDark ? AppColors.darkBorder : AppColors.lightBorder),
        borderRadius: BorderRadius.circular(16),
      ),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Wrap(
            spacing: 8,
            runSpacing: 8,
            crossAxisAlignment: WrapCrossAlignment.center,
            children: [
              Container(
                padding: const EdgeInsets.symmetric(horizontal: 10, vertical: 4),
                decoration: BoxDecoration(
                  color: statusColor.withValues(alpha: isDark ? 0.18 : 0.1),
                  borderRadius: BorderRadius.circular(20),
                ),
                child: Text(_product.isPublished ? 'Objavljen' : 'Nacrt',
                    style: TextStyle(fontSize: 12, fontWeight: FontWeight.w600, color: statusColorDark)),
              ),
              Container(
                padding: const EdgeInsets.symmetric(horizontal: 10, vertical: 4),
                decoration: BoxDecoration(
                  color: primary.withValues(alpha: isDark ? 0.18 : 0.1),
                  borderRadius: BorderRadius.circular(20),
                ),
                child: Text(_product.categoryName,
                    style: TextStyle(fontSize: 12, fontWeight: FontWeight.w600, color: primary)),
              ),
              Container(
                padding: const EdgeInsets.symmetric(horizontal: 10, vertical: 4),
                decoration: BoxDecoration(
                  color: textTertiary.withValues(alpha: 0.12),
                  borderRadius: BorderRadius.circular(20),
                ),
                child: Text(_product.ticketingMode.label,
                    style: TextStyle(fontSize: 12, fontWeight: FontWeight.w600, color: textTertiary)),
              ),
            ],
          ),
          const SizedBox(height: 12),
          if (_product.description.isNotEmpty)
            Text(_product.description, style: TextStyle(fontSize: 14, color: textTertiary, height: 1.4)),
          const SizedBox(height: 14),
          Row(
            mainAxisSize: MainAxisSize.min,
            children: [
              Icon(LucideIcons.mapPin, size: 15, color: textTertiary),
              const SizedBox(width: 6),
              Text('Lokacija: ${_product.city.label}', style: TextStyle(fontSize: 13, color: textTertiary)),
            ],
          ),
          const SizedBox(height: 8),
          ProductLocationPicker(
            initialLatitude: _product.latitude,
            initialLongitude: _product.longitude,
            editable: false,
          ),
          if (_product.date != null) ...[
            const SizedBox(height: 10),
            Row(
              mainAxisSize: MainAxisSize.min,
              children: [
                Icon(LucideIcons.calendarDays, size: 15, color: textTertiary),
                const SizedBox(width: 6),
                Text(_formatDateTime(_product.date!), style: TextStyle(fontSize: 13, color: textTertiary)),
              ],
            ),
          ],
        ],
      ),
    );
  }

  Widget _buildImagesSection(bool isDark, Color textPrimary, Color textTertiary, Color primary) {
    final images = _product.images;
    final borderColor = isDark ? AppColors.darkBorder : AppColors.lightBorder;

    return Container(
      padding: const EdgeInsets.all(20),
      decoration: BoxDecoration(
        color: isDark ? AppColors.darkSurface : Colors.white,
        border: Border.all(color: borderColor),
        borderRadius: BorderRadius.circular(16),
      ),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Text('Slike (${images.length}/$_maxImageCount)',
              style: TextStyle(fontSize: 16, fontWeight: FontWeight.w700, color: textPrimary)),
          const SizedBox(height: 4),
          Text('PNG ili JPEG, maksimalno 2MB po slici.', style: TextStyle(fontSize: 12, color: textTertiary)),
          const SizedBox(height: 14),
          Wrap(
            spacing: 12,
            runSpacing: 12,
            children: [
              for (final image in images)
                _ImageTile(
                  image: image,
                  isDark: isDark,
                  disabled: _isMutatingImage,
                  onDelete: () => _deleteImage(image),
                ),
              if (images.length < _maxImageCount)
                _AddImageTile(
                  isDark: isDark,
                  primary: primary,
                  disabled: _isMutatingImage,
                  onTap: _addImage,
                ),
            ],
          ),
        ],
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

class _ImageTile extends StatelessWidget {
  final ProductImageResponse image;
  final bool isDark;
  final bool disabled;
  final VoidCallback onDelete;

  const _ImageTile({
    required this.image,
    required this.isDark,
    required this.disabled,
    required this.onDelete,
  });

  @override
  Widget build(BuildContext context) {
    final borderColor = isDark ? AppColors.darkBorder : AppColors.lightBorder;
    return SizedBox(
      width: 100,
      height: 100,
      child: Stack(
        children: [
          ClipRRect(
            borderRadius: BorderRadius.circular(12),
            child: Image.network(
              image.url,
              width: 100,
              height: 100,
              fit: BoxFit.cover,
              errorBuilder: (context, error, stackTrace) => Container(
                width: 100,
                height: 100,
                decoration: BoxDecoration(
                  color: isDark ? AppColors.darkSurfaceMuted : AppColors.lightSurfaceMuted,
                  borderRadius: BorderRadius.circular(12),
                  border: Border.all(color: borderColor),
                ),
                child: Icon(LucideIcons.imageOff, size: 20, color: isDark ? AppColors.darkTextTertiary : AppColors.lightTextTertiary),
              ),
            ),
          ),
          Positioned(
            top: 4,
            right: 4,
            child: Material(
              color: Colors.black54,
              shape: const CircleBorder(),
              child: InkWell(
                customBorder: const CircleBorder(),
                onTap: disabled ? null : onDelete,
                child: const Padding(
                  padding: EdgeInsets.all(4),
                  child: Icon(LucideIcons.x, size: 14, color: Colors.white),
                ),
              ),
            ),
          ),
        ],
      ),
    );
  }
}

class _AddImageTile extends StatelessWidget {
  final bool isDark;
  final Color primary;
  final bool disabled;
  final VoidCallback onTap;

  const _AddImageTile({
    required this.isDark,
    required this.primary,
    required this.disabled,
    required this.onTap,
  });

  @override
  Widget build(BuildContext context) {
    final borderColor = isDark ? AppColors.darkBorderInput : AppColors.lightBorderInput;
    return Opacity(
      opacity: disabled ? 0.5 : 1,
      child: Material(
        color: Colors.transparent,
        child: InkWell(
          borderRadius: BorderRadius.circular(12),
          onTap: disabled ? null : onTap,
          child: DottedBorderTile(
            width: 100,
            height: 100,
            color: borderColor,
            child: Icon(LucideIcons.plus, size: 24, color: primary),
          ),
        ),
      ),
    );
  }
}

/// Simple dashed-border box (no external "dotted border" package dependency) — draws a plain
/// solid rounded rectangle border, which is visually enough to signal "empty add slot" alongside
/// the plus icon; a true dashed stroke isn't worth pulling in a new package for.
class DottedBorderTile extends StatelessWidget {
  final double width;
  final double height;
  final Color color;
  final Widget child;

  const DottedBorderTile({
    super.key,
    required this.width,
    required this.height,
    required this.color,
    required this.child,
  });

  @override
  Widget build(BuildContext context) {
    return Container(
      width: width,
      height: height,
      alignment: Alignment.center,
      decoration: BoxDecoration(
        border: Border.all(color: color, width: 1.5),
        borderRadius: BorderRadius.circular(12),
      ),
      child: child,
    );
  }
}

class _SectorTile extends StatelessWidget {
  final SectorResponse sector;
  final bool isDark;
  final VoidCallback onEdit;
  final VoidCallback onPublish;
  final VoidCallback onDelete;

  const _SectorTile({
    required this.sector,
    required this.isDark,
    required this.onEdit,
    required this.onPublish,
    required this.onDelete,
  });

  @override
  Widget build(BuildContext context) {
    final textPrimary = isDark ? AppColors.darkTextPrimary : AppColors.lightTextPrimary;
    final textTertiary = isDark ? AppColors.darkTextTertiary : AppColors.lightTextTertiary;
    final statusColor = sector.isPublished ? AppColors.success : AppColors.warning;
    final statusColorDark = sector.isPublished ? AppColors.successDark : AppColors.warningDark;

    var subtitle = 'Kapacitet: ${sector.capacity} · Cijena: ${sector.price.toStringAsFixed(2)} KM';
    if (sector.periodYear != null && sector.periodMonth != null) {
      subtitle += ' · Period: ${_monthName(sector.periodMonth!)} ${sector.periodYear}';
    }

    return Container(
      margin: const EdgeInsets.only(bottom: 10),
      padding: const EdgeInsets.all(14),
      decoration: BoxDecoration(
        color: isDark ? AppColors.darkSurface : Colors.white,
        borderRadius: BorderRadius.circular(14),
        border: Border.all(color: isDark ? AppColors.darkBorder : AppColors.lightBorder),
      ),
      child: Row(
        children: [
          Expanded(
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Row(
                  children: [
                    Flexible(
                      child: Text(sector.name,
                          style: TextStyle(fontSize: 14, fontWeight: FontWeight.w700, color: textPrimary),
                          maxLines: 1,
                          overflow: TextOverflow.ellipsis),
                    ),
                    const SizedBox(width: 8),
                    Container(
                      padding: const EdgeInsets.symmetric(horizontal: 8, vertical: 3),
                      decoration: BoxDecoration(
                        color: statusColor.withValues(alpha: isDark ? 0.18 : 0.1),
                        borderRadius: BorderRadius.circular(20),
                      ),
                      child: Text(sector.isPublished ? 'Objavljen' : 'Nacrt',
                          style: TextStyle(fontSize: 10, fontWeight: FontWeight.w600, color: statusColorDark)),
                    ),
                  ],
                ),
                const SizedBox(height: 4),
                Text(subtitle, style: TextStyle(fontSize: 12, color: textTertiary)),
              ],
            ),
          ),
          if (!sector.isPublished)
            IconButton(
              tooltip: 'Objavi',
              onPressed: onPublish,
              icon: Icon(LucideIcons.send, size: 16, color: AppColors.successDark),
            ),
          IconButton(
            tooltip: 'Uredi',
            onPressed: onEdit,
            icon: Icon(LucideIcons.pencil, size: 16, color: isDark ? AppColors.secondary : AppColors.primary),
          ),
          IconButton(
            tooltip: 'Obriši',
            onPressed: onDelete,
            icon: const Icon(LucideIcons.trash2, size: 16, color: AppColors.error),
          ),
        ],
      ),
    );
  }
}

String _monthName(int m) {
  const months = ['', 'Januar', 'Februar', 'Mart', 'April', 'Maj', 'Juni', 'Juli', 'Avgust', 'Septembar', 'Oktobar', 'Novembar', 'Decembar'];
  return months[m];
}
