import 'package:flutter/material.dart';
import 'package:lucide_icons_flutter/lucide_icons.dart';

import '../models/responses/product_response.dart';
import '../models/responses/sector_response.dart';
import '../models/search_objects/sector_search_object.dart';
import '../providers/product_provider.dart';
import '../providers/sector_provider.dart';
import '../theme/app_colors.dart';
import 'widgets/product_upsert_dialog.dart';
import 'widgets/sector_upsert_dialog.dart';
import '../main.dart';

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
        onSaved: () async {
          // No GET /products/{id} exists — the org's product list already has
          // the fresh copy after an edit, so re-derive it from GetMineAsync
          // scoped to this id instead of a second endpoint just for this.
          try {
            final refreshed = await ProductProvider().getMine();
            final match = refreshed.items.where((p) => p.id == _product.id);
            if (mounted && match.isNotEmpty) setState(() => _product = match.first);
          } catch (_) {
            // Non-fatal — the list screen will show the fresh copy regardless.
          }
        },
      ),
    );
  }

  Future<void> _publishProduct() async {
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

  Future<void> _deleteProduct() async {
    final confirmed = await _confirm('Obriši proizvod', 'Da li ste sigurni da želite obrisati proizvod "${_product.name}"?');
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
    final confirmed = await _confirm('Obriši sektor', 'Da li ste sigurni da želite obrisati sektor "${sector.name}"?');
    if (confirmed != true || !mounted) return;

    try {
      await _sectorProvider.delete(sector.id);
      if (mounted) await _loadSectors();
    } catch (e) {
      if (mounted) handleApiError(e);
    }
  }

  Future<bool?> _confirm(String title, String message) {
    final isDark = _isDark;
    return showDialog<bool>(
      context: context,
      builder: (ctx) => AlertDialog(
        backgroundColor: isDark ? AppColors.darkSurface : Colors.white,
        shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(16)),
        title: Text(title,
            style: TextStyle(fontWeight: FontWeight.w700, color: isDark ? AppColors.darkTextPrimary : AppColors.lightTextPrimary)),
        content: Text(message,
            style: TextStyle(color: isDark ? AppColors.darkTextSecondary : AppColors.lightTextSecondary)),
        actions: [
          TextButton(
            onPressed: () => Navigator.of(ctx).pop(false),
            child: Text('Odustani', style: TextStyle(color: isDark ? AppColors.darkTextTertiary : AppColors.lightTextTertiary)),
          ),
          ElevatedButton(
            style: ElevatedButton.styleFrom(
              backgroundColor: AppColors.errorDark,
              foregroundColor: Colors.white,
              shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(10)),
            ),
            onPressed: () => Navigator.of(ctx).pop(true),
            child: const Text('Obriši'),
          ),
        ],
      ),
    );
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
}

String _formatDateTime(DateTime date) {
  final d = date.toLocal();
  const months = ['', 'jan', 'feb', 'mar', 'apr', 'maj', 'jun', 'jul', 'aug', 'sep', 'okt', 'nov', 'dec'];
  final hh = d.hour.toString().padLeft(2, '0');
  final mm = d.minute.toString().padLeft(2, '0');
  return '${d.day}. ${months[d.month]} ${d.year}. u $hh:$mm';
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
