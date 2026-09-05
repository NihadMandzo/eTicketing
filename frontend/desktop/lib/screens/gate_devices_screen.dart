import 'dart:async';

import 'package:flutter/material.dart';
import 'package:lucide_icons_flutter/lucide_icons.dart';

import '../main.dart';
import '../models/responses/gate_device_response.dart';
import '../models/responses/product_response.dart';
import '../models/search_objects/gate_device_search_object.dart';
import '../models/search_objects/product_search_object.dart';
import '../providers/gate_device_provider.dart';
import '../providers/product_provider.dart';
import '../theme/app_colors.dart';
import '../widgets/confirm_dialog.dart';
import 'widgets/gate_device_key_dialog.dart';
import 'widgets/gate_device_upsert_dialog.dart';
import 'widgets/pagination_bar.dart';
import 'widgets/paginated_screen_body.dart';

/// The physical gate scanners (see `IoT/`) an organizer has registered.
///
/// Each card shows what that door actually admits — the product and its sector scope — because that
/// is the thing an organizer needs to confirm before opening the gates, and the only thing that
/// determines what the device lets through. The device's own firmware holds no scope at all.
class GateDevicesScreen extends StatefulWidget {
  const GateDevicesScreen({super.key});

  @override
  State<GateDevicesScreen> createState() => _GateDevicesScreenState();
}

class _GateDevicesScreenState extends State<GateDevicesScreen> {
  final _searchController = TextEditingController();
  final _provider = GateDeviceProvider();
  Timer? _debounce;

  List<GateDeviceResponse> _devices = [];
  bool _isLoading = true;

  List<ProductResponse> _products = [];
  String? _selectedProductId;

  int _currentPage = 0;
  int _totalCount = 0;
  int _pageSize = 10;

  int get _totalPages => (_totalCount / _pageSize).ceil().clamp(1, 99999);

  bool get _isDark => Theme.of(context).brightness == Brightness.dark;

  @override
  void initState() {
    super.initState();
    _loadProducts();
    _loadData();
  }

  @override
  void dispose() {
    _debounce?.cancel();
    _searchController.dispose();
    super.dispose();
  }

  Future<void> _loadProducts() async {
    try {
      final provider = ProductProvider();
      final all = <ProductResponse>[];
      var page = 0;
      const pageSize = 100;
      while (true) {
        final result = await provider.getMine(searchObject: ProductSearchObject(page: page, pageSize: pageSize));
        all.addAll(result.items);
        if (result.items.isEmpty || all.length >= result.totalCount) break;
        page++;
      }
      if (mounted) setState(() => _products = all);
    } catch (_) {
      // Non-fatal — only the product filter and the card subtitles degrade. The device list below
      // surfaces its own errors.
    }
  }

  String _productName(String productId) {
    for (final product in _products) {
      if (product.id == productId) return product.name;
    }
    return 'Nepoznat proizvod';
  }

  Future<void> _loadData() async {
    setState(() => _isLoading = true);
    try {
      final result = await _provider.getAll(
        searchObject: GateDeviceSearchObject(
          page: _currentPage,
          pageSize: _pageSize,
          fts: _searchController.text.trim().isEmpty ? null : _searchController.text.trim(),
          productId: _selectedProductId,
        ),
        fromJson: GateDeviceResponse.fromJson,
      );
      if (mounted) {
        setState(() {
          _devices = result.items;
          _totalCount = result.totalCount;
          _isLoading = false;
        });
      }
    } catch (e) {
      if (mounted) {
        setState(() => _isLoading = false);
        handleApiError(e);
      }
    }
  }

  void _onSearchChanged(String _) {
    if (_debounce?.isActive ?? false) _debounce!.cancel();
    _debounce = Timer(const Duration(milliseconds: 300), () {
      setState(() => _currentPage = 0);
      _loadData();
    });
  }

  void _onProductFilterChanged(String? productId) {
    setState(() {
      _selectedProductId = productId;
      _currentPage = 0;
    });
    _loadData();
  }

  void _openCreateDialog() {
    showDialog(
      context: context,
      barrierDismissible: false,
      builder: (_) => GateDeviceUpsertDialog(onSaved: _loadData),
    );
  }

  void _openEditDialog(GateDeviceResponse device) {
    showDialog(
      context: context,
      barrierDismissible: false,
      builder: (_) => GateDeviceUpsertDialog(device: device, onSaved: _loadData),
    );
  }

  Future<void> _rotateKey(GateDeviceResponse device) async {
    final confirmed = await ConfirmDialog.show(
      context,
      title: 'Generiši novi ključ',
      message: 'Stari ključ za "${device.name}" odmah prestaje važiti i uređaj neće raditi dok mu '
          'ne upišete novi ključ. Nastaviti?',
      confirmLabel: 'Generiši',
    );
    if (confirmed != true || !mounted) return;

    try {
      final rotated = await _provider.rotateKey(device.id);
      if (!mounted) return;
      await _loadData();
      if (!mounted) return;
      await GateDeviceKeyDialog.show(
        context,
        deviceName: rotated.device.name,
        apiKey: rotated.apiKey,
        isRotation: true,
      );
    } catch (e) {
      if (mounted) handleApiError(e);
    }
  }

  Future<void> _deleteDevice(GateDeviceResponse device) async {
    final confirmed = await ConfirmDialog.show(
      context,
      title: 'Obriši ulazni uređaj',
      message: 'Da li ste sigurni da želite obrisati "${device.name}"? Ova radnja je trajna.',
      confirmLabel: 'Obriši',
    );
    if (confirmed != true || !mounted) return;

    try {
      await _provider.delete(device.id);
      if (!mounted) return;
      handleApiSuccess('Uređaj je obrisan.');

      // Deleting the last row on a page would otherwise land the user on an empty page.
      if (_devices.length == 1 && _currentPage > 0) {
        setState(() => _currentPage--);
      }
      await _loadData();
    } catch (e) {
      if (mounted) handleApiError(e);
    }
  }

  void _goToPage(int page) {
    if (page < 0 || page >= _totalPages) return;
    setState(() => _currentPage = page);
    _loadData();
  }

  void _onPageSizeChanged(int size) {
    setState(() {
      _pageSize = size;
      _currentPage = 0;
    });
    _loadData();
  }

  @override
  Widget build(BuildContext context) {
    final isDark = _isDark;
    final primary = isDark ? AppColors.secondary : AppColors.primary;
    final primaryDark = isDark ? AppColors.primary : AppColors.primaryDark;

    return PaginatedScreenBody(
      pagination: PaginationBar(
        currentPage: _currentPage,
        totalPages: _totalPages,
        onPageChanged: _goToPage,
        pageSize: _pageSize,
        onPageSizeChanged: _onPageSizeChanged,
      ),
      content: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Row(
            mainAxisAlignment: MainAxisAlignment.spaceBetween,
            children: [
              Expanded(
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    Text(
                      'Ulazni uređaji',
                      style: TextStyle(
                        fontSize: 30,
                        fontWeight: FontWeight.w700,
                        color: isDark ? AppColors.darkTextPrimary : AppColors.lightTextPrimary,
                        height: 1.2,
                      ),
                    ),
                    const SizedBox(height: 8),
                    Text(
                      'Skeneri ulaznica na ulazima — svaki uređaj propušta samo sektore koje mu ovdje dodijelite',
                      style: TextStyle(
                        fontSize: 16,
                        color: isDark ? AppColors.darkTextSecondary : AppColors.lightTextSecondary,
                      ),
                    ),
                  ],
                ),
              ),
              const SizedBox(width: 16),
              Container(
                decoration: BoxDecoration(
                  gradient: LinearGradient(colors: [primary, primaryDark]),
                  borderRadius: BorderRadius.circular(12),
                  boxShadow: [
                    BoxShadow(color: primary.withValues(alpha: 0.2), blurRadius: 10, offset: const Offset(0, 4)),
                  ],
                ),
                child: Material(
                  color: Colors.transparent,
                  child: InkWell(
                    borderRadius: BorderRadius.circular(12),
                    onTap: _openCreateDialog,
                    child: const Padding(
                      padding: EdgeInsets.symmetric(horizontal: 16, vertical: 10),
                      child: Row(
                        children: [
                          Icon(LucideIcons.plus, color: Colors.white, size: 20),
                          SizedBox(width: 8),
                          Text('Novi uređaj', style: TextStyle(color: Colors.white, fontWeight: FontWeight.w600)),
                        ],
                      ),
                    ),
                  ),
                ),
              ),
            ],
          ),
          const SizedBox(height: 24),

          // Search + product filter — side by side above ~760px, stacked below, matching
          // ProductsScreen so narrow windows never overflow.
          LayoutBuilder(
            builder: (context, constraints) {
              final searchField = Container(
                height: 44,
                decoration: BoxDecoration(
                  color: isDark ? AppColors.darkSurface : Colors.white,
                  border: Border.all(color: isDark ? AppColors.darkBorderInput : AppColors.lightBorderInput),
                  borderRadius: BorderRadius.circular(12),
                ),
                child: TextField(
                  controller: _searchController,
                  onChanged: _onSearchChanged,
                  decoration: InputDecoration(
                    hintText: 'Pretražite uređaje...',
                    hintStyle: TextStyle(color: isDark ? AppColors.darkTextTertiary : AppColors.lightTextTertiary),
                    prefixIcon: Icon(
                      LucideIcons.search,
                      color: isDark ? AppColors.darkTextDisabled : AppColors.lightTextDisabled,
                      size: 18,
                    ),
                    border: InputBorder.none,
                    contentPadding: const EdgeInsets.symmetric(vertical: 12),
                  ),
                  style: TextStyle(color: isDark ? AppColors.darkTextPrimary : AppColors.lightTextPrimary),
                ),
              );

              final productFilter = Container(
                height: 44,
                padding: const EdgeInsets.symmetric(horizontal: 12),
                decoration: BoxDecoration(
                  color: isDark ? AppColors.darkSurface : Colors.white,
                  border: Border.all(color: isDark ? AppColors.darkBorderInput : AppColors.lightBorderInput),
                  borderRadius: BorderRadius.circular(12),
                ),
                child: DropdownButtonHideUnderline(
                  child: DropdownButton<String?>(
                    isExpanded: true,
                    value: _selectedProductId,
                    icon: const Icon(LucideIcons.chevronDown, size: 16),
                    items: [
                      const DropdownMenuItem(value: null, child: Text('Svi proizvodi')),
                      for (final product in _products)
                        DropdownMenuItem(
                          value: product.id,
                          child: Text(product.name, overflow: TextOverflow.ellipsis),
                        ),
                    ],
                    onChanged: _onProductFilterChanged,
                  ),
                ),
              );

              if (constraints.maxWidth >= 760) {
                return Row(
                  children: [
                    Expanded(flex: 2, child: searchField),
                    const SizedBox(width: 12),
                    Expanded(child: productFilter),
                  ],
                );
              }
              return Column(
                children: [searchField, const SizedBox(height: 10), productFilter],
              );
            },
          ),
          const SizedBox(height: 12),

          if (!_isLoading)
            Padding(
              padding: const EdgeInsets.only(bottom: 8),
              child: Text(
                '$_totalCount uređaja',
                style: TextStyle(
                  fontSize: 13,
                  color: isDark ? AppColors.darkTextTertiary : AppColors.lightTextTertiary,
                ),
              ),
            ),

          if (_isLoading)
            SizedBox(height: 300, child: Center(child: CircularProgressIndicator(color: primary)))
          else if (_devices.isEmpty)
            SizedBox(
              height: 300,
              child: Center(
                child: Column(
                  mainAxisSize: MainAxisSize.min,
                  children: [
                    Icon(
                      LucideIcons.scanLine,
                      size: 48,
                      color: isDark ? AppColors.darkBorderInput : AppColors.lightBorderInput,
                    ),
                    const SizedBox(height: 12),
                    Text(
                      'Nema registrovanih uređaja',
                      style: TextStyle(
                        color: isDark ? AppColors.darkTextTertiary : AppColors.lightTextTertiary,
                        fontSize: 16,
                      ),
                    ),
                  ],
                ),
              ),
            )
          else
            GridView.builder(
              shrinkWrap: true,
              physics: const NeverScrollableScrollPhysics(),
              gridDelegate: const SliverGridDelegateWithMaxCrossAxisExtent(
                maxCrossAxisExtent: 360,
                mainAxisExtent: 280,
                crossAxisSpacing: 14,
                mainAxisSpacing: 14,
              ),
              itemCount: _devices.length,
              itemBuilder: (context, index) {
                final device = _devices[index];
                return _GateDeviceCard(
                  device: device,
                  productName: _productName(device.productId),
                  isDark: isDark,
                  onEdit: () => _openEditDialog(device),
                  onRotate: () => _rotateKey(device),
                  onDelete: () => _deleteDevice(device),
                );
              },
            ),
        ],
      ),
    );
  }
}

class _GateDeviceCard extends StatelessWidget {
  final GateDeviceResponse device;
  final String productName;
  final bool isDark;
  final VoidCallback onEdit;
  final VoidCallback onRotate;
  final VoidCallback onDelete;

  const _GateDeviceCard({
    required this.device,
    required this.productName,
    required this.isDark,
    required this.onEdit,
    required this.onRotate,
    required this.onDelete,
  });

  String get _lastSeenLabel {
    final lastSeen = device.lastSeenAt;
    if (lastSeen == null) return 'Nikad se nije javio';

    final difference = DateTime.now().difference(lastSeen.toLocal());
    if (difference.inMinutes < 1) return 'Aktivan upravo sada';
    if (difference.inMinutes < 60) return 'Zadnji put prije ${difference.inMinutes} min';
    if (difference.inHours < 24) return 'Zadnji put prije ${difference.inHours} h';
    return 'Zadnji put prije ${difference.inDays} d';
  }

  @override
  Widget build(BuildContext context) {
    final primary = isDark ? AppColors.secondary : AppColors.primary;

    return Container(
      padding: const EdgeInsets.all(16),
      decoration: BoxDecoration(
        color: isDark ? AppColors.darkSurface : Colors.white,
        border: Border.all(color: isDark ? AppColors.darkBorder : AppColors.lightBorder),
        borderRadius: BorderRadius.circular(14),
      ),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Row(
            children: [
              Expanded(
                child: Text(
                  device.name,
                  maxLines: 1,
                  overflow: TextOverflow.ellipsis,
                  style: TextStyle(
                    fontSize: 16,
                    fontWeight: FontWeight.w700,
                    color: isDark ? AppColors.darkTextPrimary : AppColors.lightTextPrimary,
                  ),
                ),
              ),
              const SizedBox(width: 8),
              _StatusPill(isActive: device.isActive, isDark: isDark),
            ],
          ),
          const SizedBox(height: 4),
          Text(
            productName,
            maxLines: 1,
            overflow: TextOverflow.ellipsis,
            style: TextStyle(
              fontSize: 13,
              color: isDark ? AppColors.darkTextSecondary : AppColors.lightTextSecondary,
            ),
          ),
          const SizedBox(height: 12),

          // The scope. This is what the gate actually enforces, so it gets the most space.
          Expanded(
            child: SingleChildScrollView(
              child: device.allSectors
                  ? _ScopeChip(label: 'Svi sektori', emphasised: true, isDark: isDark)
                  : Wrap(
                      spacing: 6,
                      runSpacing: 6,
                      children: [
                        for (final sector in device.sectors)
                          _ScopeChip(label: sector.name, emphasised: false, isDark: isDark),
                      ],
                    ),
            ),
          ),
          const SizedBox(height: 8),
          Row(
            children: [
              Icon(
                LucideIcons.activity,
                size: 13,
                color: isDark ? AppColors.darkTextTertiary : AppColors.lightTextTertiary,
              ),
              const SizedBox(width: 6),
              Expanded(
                child: Text(
                  _lastSeenLabel,
                  maxLines: 1,
                  overflow: TextOverflow.ellipsis,
                  style: TextStyle(
                    fontSize: 12,
                    color: isDark ? AppColors.darkTextTertiary : AppColors.lightTextTertiary,
                  ),
                ),
              ),
            ],
          ),
          const SizedBox(height: 4),
          Row(
            children: [
              Expanded(
                child: Text(
                  '${device.keyPrefix}…',
                  maxLines: 1,
                  overflow: TextOverflow.ellipsis,
                  style: TextStyle(
                    fontFamily: 'monospace',
                    fontSize: 11.5,
                    color: isDark ? AppColors.darkTextDisabled : AppColors.lightTextDisabled,
                  ),
                ),
              ),
              IconButton(
                tooltip: 'Izmijeni',
                visualDensity: VisualDensity.compact,
                onPressed: onEdit,
                icon: Icon(LucideIcons.pencil, size: 17, color: primary),
              ),
              IconButton(
                tooltip: 'Novi ključ',
                visualDensity: VisualDensity.compact,
                onPressed: onRotate,
                icon: Icon(LucideIcons.keyRound, size: 17, color: primary),
              ),
              IconButton(
                tooltip: 'Obriši',
                visualDensity: VisualDensity.compact,
                onPressed: onDelete,
                icon: const Icon(LucideIcons.trash2, size: 17, color: AppColors.error),
              ),
            ],
          ),
        ],
      ),
    );
  }
}

class _StatusPill extends StatelessWidget {
  final bool isActive;
  final bool isDark;

  const _StatusPill({required this.isActive, required this.isDark});

  @override
  Widget build(BuildContext context) {
    final color = isActive ? AppColors.success : AppColors.error;
    return Container(
      padding: const EdgeInsets.symmetric(horizontal: 8, vertical: 3),
      decoration: BoxDecoration(
        color: color.withValues(alpha: isDark ? 0.18 : 0.12),
        borderRadius: BorderRadius.circular(20),
      ),
      child: Text(
        isActive ? 'Aktivan' : 'Neaktivan',
        style: TextStyle(fontSize: 11, fontWeight: FontWeight.w600, color: color),
      ),
    );
  }
}

class _ScopeChip extends StatelessWidget {
  final String label;
  final bool emphasised;
  final bool isDark;

  const _ScopeChip({required this.label, required this.emphasised, required this.isDark});

  @override
  Widget build(BuildContext context) {
    final primary = isDark ? AppColors.secondary : AppColors.primary;
    final color = emphasised ? primary : (isDark ? AppColors.darkTextSecondary : AppColors.lightTextSecondary);

    return Container(
      padding: const EdgeInsets.symmetric(horizontal: 10, vertical: 5),
      decoration: BoxDecoration(
        color: emphasised
            ? primary.withValues(alpha: isDark ? 0.18 : 0.10)
            : (isDark ? AppColors.darkSurfaceMuted : AppColors.lightSurfaceMuted),
        border: Border.all(color: emphasised ? primary.withValues(alpha: 0.4) : Colors.transparent),
        borderRadius: BorderRadius.circular(8),
      ),
      child: Text(
        label,
        style: TextStyle(fontSize: 12, fontWeight: FontWeight.w600, color: color),
      ),
    );
  }
}
