import 'package:flutter/material.dart';
import 'package:lucide_icons_flutter/lucide_icons.dart';

import '../../main.dart';
import '../../models/requests/gate_device_upsert_request.dart';
import '../../models/responses/gate_device_response.dart';
import '../../models/responses/product_response.dart';
import '../../models/responses/sector_response.dart';
import '../../models/search_objects/product_search_object.dart';
import '../../models/search_objects/sector_search_object.dart';
import '../../providers/api_exception.dart';
import '../../providers/gate_device_provider.dart';
import '../../providers/product_provider.dart';
import '../../providers/sector_provider.dart';
import '../../theme/app_colors.dart';
import 'gate_device_key_dialog.dart';

/// Register or re-scope a gate scanner.
///
/// The sector multi-select is the point of the screen: ticking several sectors is the supported way
/// to run one physical scanner across more than one sector, and re-ticking them later re-scopes a
/// live gate without anyone re-flashing the device — it picks the change up on its next config
/// refresh (5 minutes), or immediately on reboot.
class GateDeviceUpsertDialog extends StatefulWidget {
  final GateDeviceResponse? device;
  final VoidCallback onSaved;

  const GateDeviceUpsertDialog({super.key, this.device, required this.onSaved});

  @override
  State<GateDeviceUpsertDialog> createState() => _GateDeviceUpsertDialogState();
}

class _GateDeviceUpsertDialogState extends State<GateDeviceUpsertDialog> {
  /// Mirrors UpsertGateDeviceRequestValidator.MaxNameLength.
  static const _maxNameLength = 100;

  /// Mirrors UpsertGateDeviceRequestValidator.MaxSectors.
  static const _maxSectors = 50;

  final _formKey = GlobalKey<FormState>();
  final _nameCtrl = TextEditingController();
  final _provider = GateDeviceProvider();

  List<ProductResponse> _products = [];
  bool _isLoadingProducts = true;

  String? _productId;
  List<SectorResponse> _sectors = [];
  bool _isLoadingSectors = false;

  final Set<String> _selectedSectorIds = {};
  bool _allSectors = false;
  bool _isActive = true;
  bool _isSaving = false;

  /// Ownership/scope failures the server alone can detect (a sector that does not belong to the
  /// product, an empty scope) are shown against the sector list rather than as a generic snackbar.
  String? _sectorError;

  bool get _isEditing => widget.device != null;
  bool get _isDark => Theme.of(context).brightness == Brightness.dark;

  @override
  void initState() {
    super.initState();

    if (_isEditing) {
      final device = widget.device!;
      _nameCtrl.text = device.name;
      _productId = device.productId;
      _allSectors = device.allSectors;
      _isActive = device.isActive;
      _selectedSectorIds.addAll(device.sectors.map((s) => s.id));
    }

    _loadProducts();
  }

  @override
  void dispose() {
    _nameCtrl.dispose();
    super.dispose();
  }

  Future<void> _loadProducts() async {
    try {
      // Same "page through the server's ceiling" pattern the other dialogs use — an organizer with
      // more than one page of products still needs to see all of them in a dropdown.
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

      if (!mounted) return;
      setState(() {
        _products = all;
        _isLoadingProducts = false;
      });

      if (_productId != null) await _loadSectors(_productId!);
    } catch (e) {
      if (!mounted) return;
      setState(() => _isLoadingProducts = false);
      handleApiError(e);
    }
  }

  Future<void> _loadSectors(String productId) async {
    setState(() {
      _isLoadingSectors = true;
      _sectors = [];
    });

    try {
      final provider = SectorProvider();
      final all = <SectorResponse>[];
      var page = 0;
      const pageSize = 100;
      while (true) {
        final result = await provider.getMine(
          searchObject: SectorSearchObject(page: page, pageSize: pageSize, productId: productId),
        );
        all.addAll(result.items);
        if (result.items.isEmpty || all.length >= result.totalCount) break;
        page++;
      }

      if (!mounted) return;
      setState(() {
        _sectors = all;
        _isLoadingSectors = false;
        // Drop any selection that no longer exists on this product — the sector could have been
        // deleted since this device was registered, and sending it would just be rejected.
        _selectedSectorIds.retainWhere((id) => all.any((s) => s.id == id));
      });
    } catch (e) {
      if (!mounted) return;
      setState(() => _isLoadingSectors = false);
      handleApiError(e);
    }
  }

  void _onProductChanged(String? productId) {
    if (productId == null || productId == _productId) return;
    setState(() {
      _productId = productId;
      _selectedSectorIds.clear();
      _sectorError = null;
    });
    _loadSectors(productId);
  }

  /// Client-side mirror of the backend's scope rule. The server re-checks regardless — this only
  /// saves a round trip and gives a message next to the offending control.
  String? _validateScope() {
    if (_allSectors) return null;
    if (_selectedSectorIds.isEmpty) {
      return "Odaberite najmanje jedan sektor ili uključite opciju 'Svi sektori'.";
    }
    if (_selectedSectorIds.length > _maxSectors) {
      return 'Uređaj može pokrivati najviše $_maxSectors sektora.';
    }
    return null;
  }

  Future<void> _save() async {
    final scopeError = _validateScope();
    setState(() => _sectorError = scopeError);

    if (!(_formKey.currentState?.validate() ?? false) || scopeError != null) return;

    setState(() => _isSaving = true);

    final request = GateDeviceUpsertRequest(
      productId: _productId!,
      name: _nameCtrl.text.trim(),
      allSectors: _allSectors,
      sectorIds: _selectedSectorIds.toList(),
      isActive: _isActive,
    );

    try {
      if (_isEditing) {
        await _provider.updateDevice(widget.device!.id, request);
        if (!mounted) return;
        handleApiSuccess('Uređaj je sačuvan.');
        Navigator.of(context).pop();
        widget.onSaved();
      } else {
        final created = await _provider.createDevice(request);
        if (!mounted) return;
        Navigator.of(context).pop();
        widget.onSaved();
        await GateDeviceKeyDialog.show(
          context,
          deviceName: created.device.name,
          apiKey: created.apiKey,
        );
      }
    } catch (e) {
      if (!mounted) return;
      setState(() => _isSaving = false);

      // Scope problems belong on the sector list; everything else is a snackbar.
      const scopeCodes = {'gate_device.sector_not_in_product', 'gate_device.no_sectors'};
      if (e is ApiException && scopeCodes.contains(e.apiError.code)) {
        setState(() => _sectorError = e.apiError.displayMessage);
        return;
      }
      handleApiError(e);
    }
  }

  @override
  Widget build(BuildContext context) {
    final isDark = _isDark;
    final primary = isDark ? AppColors.secondary : AppColors.primary;

    return AlertDialog(
      backgroundColor: isDark ? AppColors.darkSurface : Colors.white,
      shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(16)),
      title: Row(
        children: [
          Icon(LucideIcons.scanLine, color: primary, size: 22),
          const SizedBox(width: 10),
          Expanded(
            child: Text(
              _isEditing ? 'Izmijeni ulazni uređaj' : 'Novi ulazni uređaj',
              style: TextStyle(
                fontWeight: FontWeight.w700,
                color: isDark ? AppColors.darkTextPrimary : AppColors.lightTextPrimary,
              ),
            ),
          ),
        ],
      ),
      content: ConstrainedBox(
        constraints: const BoxConstraints(maxWidth: 560),
        child: SingleChildScrollView(
          child: Form(
            key: _formKey,
            child: Column(
              mainAxisSize: MainAxisSize.min,
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                TextFormField(
                  controller: _nameCtrl,
                  maxLength: _maxNameLength,
                  decoration: const InputDecoration(
                    labelText: 'Naziv uređaja',
                    hintText: 'npr. Ulaz A — VIP i Loža',
                    border: OutlineInputBorder(),
                  ),
                  validator: (value) {
                    final name = value?.trim() ?? '';
                    if (name.isEmpty) return 'Naziv uređaja je obavezan.';
                    if (name.length > _maxNameLength) {
                      return 'Naziv može imati najviše $_maxNameLength znakova.';
                    }
                    return null;
                  },
                ),
                const SizedBox(height: 8),
                if (_isLoadingProducts)
                  const Padding(
                    padding: EdgeInsets.symmetric(vertical: 12),
                    child: LinearProgressIndicator(),
                  )
                else
                  DropdownButtonFormField<String>(
                    initialValue: _productId,
                    isExpanded: true,
                    decoration: const InputDecoration(
                      labelText: 'Proizvod',
                      border: OutlineInputBorder(),
                    ),
                    items: [
                      for (final product in _products)
                        DropdownMenuItem(
                          value: product.id,
                          child: Text(product.name, overflow: TextOverflow.ellipsis),
                        ),
                    ],
                    // Immutable after creation, exactly like Sector.ProductId: re-pointing a gate at
                    // another event would silently orphan its sector scope, so it is a new device.
                    onChanged: _isEditing ? null : _onProductChanged,
                    validator: (value) => (value == null || value.isEmpty) ? 'Proizvod je obavezan.' : null,
                  ),
                if (_isEditing)
                  Padding(
                    padding: const EdgeInsets.only(top: 6),
                    child: Text(
                      'Proizvod se ne može promijeniti. Za drugi događaj registrujte novi uređaj.',
                      style: TextStyle(
                        fontSize: 12,
                        color: isDark ? AppColors.darkTextTertiary : AppColors.lightTextTertiary,
                      ),
                    ),
                  ),
                const SizedBox(height: 16),
                _buildSectorSection(isDark, primary),
                const SizedBox(height: 8),
                SwitchListTile(
                  contentPadding: EdgeInsets.zero,
                  value: _isActive,
                  activeThumbColor: primary,
                  onChanged: (value) => setState(() => _isActive = value),
                  title: const Text('Aktivan'),
                  subtitle: Text(
                    'Neaktivan uređaj odmah prestaje raditi, ali zadržava historiju validacija.',
                    style: TextStyle(
                      fontSize: 12,
                      color: isDark ? AppColors.darkTextTertiary : AppColors.lightTextTertiary,
                    ),
                  ),
                ),
              ],
            ),
          ),
        ),
      ),
      actions: [
        TextButton(
          onPressed: _isSaving ? null : () => Navigator.of(context).pop(),
          child: const Text('Odustani'),
        ),
        FilledButton(
          style: FilledButton.styleFrom(backgroundColor: primary),
          onPressed: _isSaving ? null : _save,
          child: _isSaving
              ? const SizedBox(width: 18, height: 18, child: CircularProgressIndicator(strokeWidth: 2))
              : Text(_isEditing ? 'Sačuvaj' : 'Registruj'),
        ),
      ],
    );
  }

  Widget _buildSectorSection(bool isDark, Color primary) {
    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        SwitchListTile(
          contentPadding: EdgeInsets.zero,
          value: _allSectors,
          activeThumbColor: primary,
          onChanged: (value) => setState(() {
            _allSectors = value;
            _sectorError = null;
          }),
          title: const Text('Svi sektori'),
          subtitle: Text(
            'Glavni ulaz — uređaj propušta ulaznice svih sektora ovog proizvoda.',
            style: TextStyle(
              fontSize: 12,
              color: isDark ? AppColors.darkTextTertiary : AppColors.lightTextTertiary,
            ),
          ),
        ),
        if (!_allSectors) ...[
          const SizedBox(height: 6),
          Text(
            'Sektori na ovom ulazu',
            style: TextStyle(
              fontSize: 13,
              fontWeight: FontWeight.w600,
              color: isDark ? AppColors.darkTextSecondary : AppColors.lightTextSecondary,
            ),
          ),
          const SizedBox(height: 6),
          Container(
            constraints: const BoxConstraints(maxHeight: 220),
            decoration: BoxDecoration(
              border: Border.all(
                color: _sectorError != null
                    ? AppColors.error
                    : (isDark ? AppColors.darkBorderInput : AppColors.lightBorderInput),
              ),
              borderRadius: BorderRadius.circular(10),
            ),
            child: _buildSectorList(isDark, primary),
          ),
          if (_sectorError != null)
            Padding(
              padding: const EdgeInsets.only(top: 6, left: 4),
              child: Text(
                _sectorError!,
                style: const TextStyle(fontSize: 12, color: AppColors.errorText),
              ),
            ),
        ],
      ],
    );
  }

  Widget _buildSectorList(bool isDark, Color primary) {
    if (_productId == null) {
      return _hint(isDark, 'Prvo odaberite proizvod.');
    }
    if (_isLoadingSectors) {
      return const Padding(
        padding: EdgeInsets.all(20),
        child: Center(child: SizedBox(width: 22, height: 22, child: CircularProgressIndicator(strokeWidth: 2))),
      );
    }
    if (_sectors.isEmpty) {
      return _hint(isDark, 'Ovaj proizvod nema nijedan sektor. Prvo kreirajte sektor.');
    }

    // A Column in a SingleChildScrollView rather than a ListView, deliberately. AlertDialog sizes
    // its content through IntrinsicWidth, which measures the whole subtree, and a lazy viewport
    // (ListView/GridView) throws outright when asked for an intrinsic dimension — instantiating
    // every child to answer is exactly what a viewport exists to avoid. SingleChildScrollView
    // delegates intrinsics straight to its child, so it measures fine, and a product's sector
    // count is small enough that laziness buys nothing here anyway.
    //
    // The enclosing Container's maxHeight is what keeps this scrollable instead of unbounded.
    return SingleChildScrollView(
      child: Column(
        mainAxisSize: MainAxisSize.min,
        children: [
          for (final sector in _sectors)
            CheckboxListTile(
              dense: true,
              value: _selectedSectorIds.contains(sector.id),
              activeColor: primary,
              controlAffinity: ListTileControlAffinity.leading,
              title: Text(sector.name, overflow: TextOverflow.ellipsis),
              subtitle: Text(
                sector.isPublished ? 'Objavljen' : 'U pripremi',
                style: TextStyle(
                  fontSize: 11,
                  color: isDark ? AppColors.darkTextTertiary : AppColors.lightTextTertiary,
                ),
              ),
              onChanged: (checked) => setState(() {
                if (checked ?? false) {
                  _selectedSectorIds.add(sector.id);
                } else {
                  _selectedSectorIds.remove(sector.id);
                }
                _sectorError = null;
              }),
            ),
        ],
      ),
    );
  }

  Widget _hint(bool isDark, String text) => Padding(
        padding: const EdgeInsets.all(16),
        child: Text(
          text,
          style: TextStyle(
            fontSize: 13,
            color: isDark ? AppColors.darkTextTertiary : AppColors.lightTextTertiary,
          ),
        ),
      );
}
