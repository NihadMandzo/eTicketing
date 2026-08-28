import 'dart:async';

import 'package:flutter/material.dart';

import '../models/responses/organization_response.dart';
import '../models/responses/product_response.dart';
import '../models/responses/sector_response.dart';
import '../models/requests/hold_sector_request.dart';
import '../services/api_exception.dart';
import '../services/cart.dart';
import '../services/catalog_service.dart';
import '../services/organization_service.dart';
import '../services/recommendation_service.dart';
import '../services/sector_service.dart';
import '../theme/app_colors.dart';
import '../widgets/product_location_map.dart';
import '../widgets/purchase_widgets.dart';
import '../widgets/recommendation_row.dart';
import '../widgets/responsive_page.dart';
import 'login_screen.dart';
import 'payment_screen.dart';

/// One purchasable row — one per Sector.TicketType, or one per Sector itself
/// when it has no TicketTypes (today's single-implicit-price case). Mirrors
/// `PurchaseRow`/`rows` on `frontend/web`'s ProductDetailsComponent.
class _PurchaseRow {
  final SectorResponse sector;
  final String? ticketTypeId;
  final String? ticketTypeName;
  final double price;

  const _PurchaseRow({
    required this.sector,
    this.ticketTypeId,
    this.ticketTypeName,
    required this.price,
  });

  String get key => '${sector.id}::${ticketTypeId ?? 'flat'}';
}

/// SingleOccurrence event details (mockup screen 4) — hero gradient, info
/// rows, description, per-Sector independent qty steppers (one Sector per
/// Redis hold, same as `frontend/web`'s ProductDetailsComponent — a buyer
/// selecting quantities across several Sectors ends up with one hold, and
/// one order, per Sector), sticky total + "Kupi ulaznice" bottom bar.
class EventDetailsScreen extends StatefulWidget {
  final String productId;

  const EventDetailsScreen({super.key, required this.productId});

  @override
  State<EventDetailsScreen> createState() => _EventDetailsScreenState();
}

class _EventDetailsScreenState extends State<EventDetailsScreen> {
  final _catalogService = CatalogService();
  final _sectorService = SectorService();
  final _organizationService = OrganizationService();
  final _recommendationService = RecommendationService();

  ProductResponse? _product;
  List<SectorResponse> _sectors = [];
  OrganizationResponse? _organization;
  bool _isLoading = true;
  String? _loadError;

  List<ProductResponse> _similar = [];
  bool _isLoadingSimilar = true;

  final Map<String, int> _quantities = {};
  bool _isSubmitting = false;
  String? _submitError;

  @override
  void initState() {
    super.initState();
    _load();
  }

  Future<void> _load() async {
    try {
      final product = await _catalogService.getProductById(widget.productId);
      final sectors = await _sectorService.getSectors(widget.productId);
      OrganizationResponse? org;
      try {
        org = await _organizationService.getById(product.organizationId);
      } catch (_) {
        // Organizer name is a nice-to-have — don't block the whole screen on it.
      }
      if (!mounted) return;
      setState(() {
        _product = product;
        _sectors = sectors.items;
        _organization = org;
        _isLoading = false;
      });

      // Both deliberately unawaited: the screen is already usable, and neither must delay it.
      unawaited(_recommendationService.trackView(product.id));
      unawaited(_loadSimilar(product.id));
    } catch (_) {
      if (!mounted) return;
      setState(() {
        _loadError = 'Događaj nije pronađen ili više nije dostupan.';
        _isLoading = false;
      });
    }
  }

  /// "Slično ovome". Swallows its own error and simply leaves the row hidden — same treatment as
  /// the organizer lookup above, for the same reason.
  Future<void> _loadSimilar(String productId) async {
    try {
      final similar = await _recommendationService.getSimilar(productId, take: 6);
      if (!mounted) return;
      setState(() {
        _similar = similar;
        _isLoadingSimilar = false;
      });
    } catch (_) {
      if (!mounted) return;
      setState(() {
        _similar = [];
        _isLoadingSimilar = false;
      });
    }
  }

  void _openProduct(ProductResponse product) {
    // Replaces rather than stacks: tapping through five similar events should not leave five
    // detail screens on the back stack for the user to dismiss one at a time.
    Navigator.of(context).pushReplacement(
      MaterialPageRoute(builder: (_) => EventDetailsScreen(productId: product.id)),
    );
  }

  List<_PurchaseRow> get _rows => _sectors.expand((sector) {
    if (sector.ticketTypes.isNotEmpty) {
      return sector.ticketTypes.map(
        (tt) => _PurchaseRow(
          sector: sector,
          ticketTypeId: tt.id,
          ticketTypeName: tt.name,
          price: tt.price,
        ),
      );
    }
    return [_PurchaseRow(sector: sector, price: sector.price)];
  }).toList();

  double get _total => _rows.fold(
    0,
    (sum, row) => sum + (_quantities[row.key] ?? 0) * row.price,
  );

  bool get _hasSelection => _quantities.values.any((q) => q > 0);

  Future<void> _proceedToCheckout() async {
    final product = _product;
    if (product == null || _isSubmitting) return;

    final bySector = <String, List<_PurchaseRow>>{};
    for (final row in _rows) {
      if ((_quantities[row.key] ?? 0) > 0) {
        bySector.putIfAbsent(row.sector.id, () => []).add(row);
      }
    }
    if (bySector.isEmpty) return;

    setState(() {
      _isSubmitting = true;
      _submitError = null;
    });

    // Declared outside the try so both catch clauses below can see (and release) whichever holds
    // already succeeded before a later Sector's hold call failed — otherwise those holds just sit
    // there ticking down their own TTL instead of being released immediately.
    final holds = <CartHoldGroup>[];
    try {
      for (final entry in bySector.entries) {
        final quantity = entry.value.fold(
          0,
          (sum, row) => sum + (_quantities[row.key] ?? 0),
        );
        final hold = await _sectorService.hold(
          entry.key,
          HoldSectorRequest(quantity: quantity),
        );
        holds.add(
          CartHoldGroup(
            holdId: hold.holdId,
            sectorId: entry.key,
            sectorName: entry.value.first.sector.name,
            lineItems: entry.value
                .map(
                  (row) => CartLineItem(
                    ticketTypeId: row.ticketTypeId,
                    ticketTypeName: row.ticketTypeName,
                    quantity: _quantities[row.key] ?? 0,
                    unitPrice: row.price,
                  ),
                )
                .toList(),
          ),
        );
      }

      Cart.set(
        CartState(
          productId: product.id,
          productName: product.name,
          eventSummary: product.date != null
              ? _formatDateTime(product.date!)
              : '',
          holds: holds,
        ),
      );

      if (!mounted) return;
      Navigator.of(
        context,
      ).push(MaterialPageRoute(builder: (_) => const PaymentScreen()));
    } on ApiException catch (e) {
      for (final hold in holds) {
        unawaited(_sectorService.release(hold.holdId));
      }
      if (!mounted) return;
      if (e.statusCode == 401) {
        Navigator.of(context).pushAndRemoveUntil(
          MaterialPageRoute(builder: (_) => const LoginScreen()),
          (route) => false,
        );
        return;
      }
      setState(() => _submitError = e.apiError.displayMessage);
    } catch (_) {
      for (final hold in holds) {
        unawaited(_sectorService.release(hold.holdId));
      }
      if (mounted) {
        setState(() => _submitError = 'Došlo je do greške. Pokušajte ponovo.');
      }
    } finally {
      if (mounted) setState(() => _isSubmitting = false);
    }
  }

  static String _formatDateTime(DateTime date) {
    const months = [
      'Januar',
      'Februar',
      'Mart',
      'April',
      'Maj',
      'Juni',
      'Juli',
      'August',
      'Septembar',
      'Oktobar',
      'Novembar',
      'Decembar',
    ];
    final hh = date.hour.toString().padLeft(2, '0');
    final mm = date.minute.toString().padLeft(2, '0');
    return '${date.day}. ${months[date.month - 1]} ${date.year} · $hh:$mm h';
  }

  @override
  Widget build(BuildContext context) {
    if (_isLoading) {
      return const Scaffold(body: Center(child: CircularProgressIndicator()));
    }
    if (_loadError != null || _product == null) {
      return Scaffold(
        appBar: AppBar(title: const Text('Detalji događaja')),
        body: Center(child: Text(_loadError ?? 'Greška')),
      );
    }

    final product = _product!;
    final isDark = Theme.of(context).brightness == Brightness.dark;
    final tertiaryText = isDark
        ? AppColors.darkTextTertiary
        : AppColors.lightTextTertiary;

    return Scaffold(
      body: Stack(
        children: [
          SafeArea(
            bottom: false,
            child: SingleChildScrollView(
              padding: const EdgeInsets.only(bottom: 100),
              child: ResponsivePage(
                padding: EdgeInsets.zero,
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    Stack(
                      children: [
                        Container(
                          height: 220,
                          width: double.infinity,
                          decoration: const BoxDecoration(
                            gradient: LinearGradient(
                              colors: [AppColors.primary, AppColors.secondary],
                              begin: Alignment.topLeft,
                              end: Alignment.bottomRight,
                            ),
                          ),
                          child: product.images.isNotEmpty
                              ? Image.network(
                                  product.images.first.url,
                                  fit: BoxFit.cover,
                                  errorBuilder: (_, _, _) =>
                                      const SizedBox.shrink(),
                                )
                              : null,
                        ),
                        Positioned(
                          top: 8,
                          left: 8,
                          child: CircleBackButton(
                            onTap: () => Navigator.of(context).pop(),
                          ),
                        ),
                      ],
                    ),
                    Padding(
                      padding: const EdgeInsets.fromLTRB(20, 20, 20, 0),
                      child: Column(
                        crossAxisAlignment: CrossAxisAlignment.start,
                        children: [
                          Row(
                            children: [
                              Container(
                                padding: const EdgeInsets.symmetric(
                                  horizontal: 10,
                                  vertical: 4,
                                ),
                                decoration: BoxDecoration(
                                  color: AppColors.accent,
                                  borderRadius: BorderRadius.circular(6),
                                ),
                                child: Text(
                                  product.categoryName,
                                  style: const TextStyle(
                                    color: Colors.white,
                                    fontSize: 11,
                                    fontWeight: FontWeight.w700,
                                  ),
                                ),
                              ),
                            ],
                          ),
                          const SizedBox(height: 12),
                          Text(
                            product.name,
                            style: const TextStyle(
                              fontSize: 22,
                              fontWeight: FontWeight.w700,
                            ),
                          ),
                          const SizedBox(height: 16),
                          if (product.date != null)
                            InfoRow(
                              icon: Icons.calendar_today_rounded,
                              text: _formatDateTime(product.date!),
                            ),
                          const SizedBox(height: 20),
                          const Divider(),
                          const SizedBox(height: 12),
                          const Text(
                            'O događaju',
                            style: TextStyle(
                              fontSize: 15,
                              fontWeight: FontWeight.w700,
                            ),
                          ),
                          const SizedBox(height: 8),
                          Text(
                            product.description.isNotEmpty
                                ? product.description
                                : 'Opis nije dostupan.',
                            style: TextStyle(
                              fontSize: 14,
                              color: tertiaryText,
                              height: 1.5,
                            ),
                          ),
                          const SizedBox(height: 20),
                          ProductLocationMap(
                            latitude: product.latitude,
                            longitude: product.longitude,
                            city: product.city,
                          ),
                          if (_organization != null) ...[
                            const SizedBox(height: 20),
                            const Text(
                              'Organizator',
                              style: TextStyle(
                                fontSize: 15,
                                fontWeight: FontWeight.w700,
                              ),
                            ),
                            const SizedBox(height: 10),
                            OrganizerCard(organization: _organization!),
                          ],
                          const SizedBox(height: 24),
                          const Text(
                            'Sektori',
                            style: TextStyle(
                              fontSize: 15,
                              fontWeight: FontWeight.w700,
                            ),
                          ),
                          const SizedBox(height: 12),
                          if (_rows.isEmpty)
                            Text(
                              'Nema dostupnih sektora za ovaj događaj.',
                              style: TextStyle(color: tertiaryText),
                            )
                          else
                            Column(
                              children: [
                                for (final row in _rows)
                                  Padding(
                                    padding: const EdgeInsets.only(bottom: 14),
                                    child: QuantityRow(
                                      title:
                                          row.ticketTypeName ?? row.sector.name,
                                      price: row.price,
                                      quantity: _quantities[row.key] ?? 0,
                                      onDecrement: () => setState(
                                        () => _quantities[row.key] =
                                            ((_quantities[row.key] ?? 0) - 1)
                                                .clamp(0, 10),
                                      ),
                                      onIncrement: () => setState(
                                        () => _quantities[row.key] =
                                            ((_quantities[row.key] ?? 0) + 1)
                                                .clamp(0, 10),
                                      ),
                                    ),
                                  ),
                              ],
                            ),
                          if (_submitError != null) ...[
                            const SizedBox(height: 8),
                            Text(
                              _submitError!,
                              style: const TextStyle(
                                color: AppColors.errorDark,
                                fontSize: 13,
                              ),
                            ),
                          ],
                        ],
                      ),
                    ),
                    Padding(
                      padding: const EdgeInsets.fromLTRB(16, 8, 16, 0),
                      child: RecommendationRow(
                        title: 'Slično ovome',
                        products: _similar,
                        isLoading: _isLoadingSimilar,
                        onProductTap: _openProduct,
                      ),
                    ),
                  ],
                ),
              ),
            ),
          ),
          if (_rows.isNotEmpty)
            Positioned(
              left: 0,
              right: 0,
              bottom: 0,
              child: PurchaseBottomBar(
                totalLabel: 'Ukupno',
                total: _total,
                buttonLabel: 'Kupi ulaznice',
                enabled: _hasSelection && !_isSubmitting,
                isLoading: _isSubmitting,
                onPressed: _proceedToCheckout,
              ),
            ),
        ],
      ),
    );
  }
}
