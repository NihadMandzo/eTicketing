import 'dart:async';

import 'package:flutter/material.dart';

import '../models/requests/hold_sector_request.dart';
import '../models/responses/organization_response.dart';
import '../models/responses/product_response.dart';
import '../models/responses/sector_response.dart';
import '../services/api_exception.dart';
import '../services/cart.dart';
import '../services/catalog_service.dart';
import '../services/organization_service.dart';
import '../services/sector_service.dart';
import '../theme/app_colors.dart';
import '../widgets/purchase_widgets.dart';
import '../widgets/responsive_page.dart';
import 'login_screen.dart';
import 'payment_screen.dart';

/// RecurringReservation product details (mockup screen 10) — flat card grid
/// of the product's real Sectors ("parking spaces"), no fabricated spatial
/// layout: tapping a card immediately attempts a live 1-qty hold against
/// that Sector; a 409 marks it "zauzeto" and prompts picking another — the
/// same mechanism every purchase path already uses. Matches
/// `ProductDetailsComponent`'s `selectSpot()` on `frontend/web`.
class ParkingSpotScreen extends StatefulWidget {
  final String productId;

  const ParkingSpotScreen({super.key, required this.productId});

  @override
  State<ParkingSpotScreen> createState() => _ParkingSpotScreenState();
}

class _ParkingSpotScreenState extends State<ParkingSpotScreen> {
  final _catalogService = CatalogService();
  final _sectorService = SectorService();
  final _organizationService = OrganizationService();

  ProductResponse? _product;
  List<SectorResponse> _sectors = [];
  OrganizationResponse? _organization;
  bool _isLoading = true;
  String? _loadError;

  String? _selectedSectorId;
  final Set<String> _takenSectorIds = {};
  bool _isSubmitting = false;
  String? _submitError;
  // The Redis holdId behind the currently-selected spot, so switching to a different spot can
  // release this one instead of leaking it for the full 5-minute TTL.
  String? _selectedSpotHoldId;

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
      } catch (_) {}
      if (!mounted) return;
      setState(() {
        _product = product;
        _sectors = sectors.items;
        _organization = org;
        _isLoading = false;
      });
    } catch (_) {
      if (!mounted) return;
      setState(() {
        _loadError = 'Parking nije pronađen ili više nije dostupan.';
        _isLoading = false;
      });
    }
  }

  Future<void> _selectSpot(SectorResponse sector) async {
    if (_isSubmitting || _takenSectorIds.contains(sector.id)) return;

    setState(() {
      _isSubmitting = true;
      _submitError = null;
    });

    try {
      final hold = await _sectorService.hold(
        sector.id,
        const HoldSectorRequest(quantity: 1),
      );
      final product = _product!;
      Cart.set(
        CartState(
          productId: product.id,
          productName: product.name,
          eventSummary: sector.name,
          holds: [
            CartHoldGroup(
              holdId: hold.holdId,
              sectorId: sector.id,
              sectorName: sector.name,
              lineItems: [CartLineItem(quantity: 1, unitPrice: sector.price)],
            ),
          ],
        ),
      );

      // Only release the previous spot's hold once the new one has actually succeeded —
      // releasing it up front and then having this call fail would leave the buyer with no held
      // spot at all.
      final previousHoldId = _selectedSpotHoldId;
      if (previousHoldId != null) {
        unawaited(_sectorService.release(previousHoldId));
      }

      if (!mounted) return;
      setState(() {
        _selectedSectorId = sector.id;
        _selectedSpotHoldId = hold.holdId;
        _isSubmitting = false;
      });
    } on ApiException catch (e) {
      if (!mounted) return;
      if (e.statusCode == 401) {
        Navigator.of(context).pushAndRemoveUntil(
          MaterialPageRoute(builder: (_) => const LoginScreen()),
          (route) => false,
        );
        return;
      }
      setState(() {
        _isSubmitting = false;
        if (e.statusCode == 409) {
          _takenSectorIds.add(sector.id);
          _submitError = 'Ovo mjesto je upravo zauzeto. Odaberite drugo.';
        } else {
          _submitError = e.apiError.displayMessage;
        }
      });
    } catch (_) {
      if (mounted) {
        setState(() {
          _isSubmitting = false;
          _submitError = 'Došlo je do greške. Pokušajte ponovo.';
        });
      }
    }
  }

  void _goToPayment() {
    if (_selectedSectorId != null) {
      Navigator.of(
        context,
      ).push(MaterialPageRoute(builder: (_) => const PaymentScreen()));
    }
  }

  @override
  Widget build(BuildContext context) {
    if (_isLoading) {
      return const Scaffold(body: Center(child: CircularProgressIndicator()));
    }
    if (_loadError != null || _product == null) {
      return Scaffold(
        appBar: AppBar(title: const Text('Parking karta')),
        body: Center(child: Text(_loadError ?? 'Greška')),
      );
    }

    final product = _product!;
    final isDark = Theme.of(context).brightness == Brightness.dark;
    final tertiaryText = isDark
        ? AppColors.darkTextTertiary
        : AppColors.lightTextTertiary;
    final selectedSector = _sectors
        .where((s) => s.id == _selectedSectorId)
        .cast<SectorResponse?>()
        .firstWhere((_) => true, orElse: () => null);

    return Scaffold(
      body: Stack(
        children: [
          SafeArea(
            bottom: false,
            child: SingleChildScrollView(
              padding: EdgeInsets.only(
                bottom: selectedSector != null ? 100 : 24,
              ),
              child: ResponsivePage(
                padding: EdgeInsets.zero,
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    Stack(
                      children: [
                        Container(
                          height: 160,
                          width: double.infinity,
                          decoration: const BoxDecoration(
                            gradient: LinearGradient(
                              colors: [AppColors.primary, AppColors.secondary],
                              begin: Alignment.topLeft,
                              end: Alignment.bottomRight,
                            ),
                          ),
                        ),
                        Positioned(
                          top: 0,
                          left: 0,
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
                              fontSize: 20,
                              fontWeight: FontWeight.w700,
                            ),
                          ),
                          if (_organization != null) ...[
                            const SizedBox(height: 12),
                            InfoRow(
                              icon: Icons.person_outline_rounded,
                              text: 'Organizator: ${_organization!.name}',
                            ),
                          ],
                          const SizedBox(height: 20),
                          const Text(
                            'Odaberite parking mjesto',
                            style: TextStyle(
                              fontSize: 15,
                              fontWeight: FontWeight.w700,
                            ),
                          ),
                          const SizedBox(height: 12),
                          if (_sectors.isEmpty)
                            Text(
                              'Nema dostupnih mjesta.',
                              style: TextStyle(color: tertiaryText),
                            )
                          else
                            GridView.builder(
                              shrinkWrap: true,
                              physics: const NeverScrollableScrollPhysics(),
                              gridDelegate:
                                  const SliverGridDelegateWithFixedCrossAxisCount(
                                    crossAxisCount: 5,
                                    mainAxisSpacing: 8,
                                    crossAxisSpacing: 8,
                                  ),
                              itemCount: _sectors.length,
                              itemBuilder: (context, index) {
                                final sector = _sectors[index];
                                final isTaken = _takenSectorIds.contains(
                                  sector.id,
                                );
                                final isSelected =
                                    sector.id == _selectedSectorId;
                                return InkWell(
                                  borderRadius: BorderRadius.circular(8),
                                  onTap: isTaken
                                      ? null
                                      : () => _selectSpot(sector),
                                  child: Container(
                                    decoration: BoxDecoration(
                                      color: isSelected
                                          ? AppColors.primary
                                          : (isDark
                                                ? AppColors.darkSurfaceMuted
                                                : const Color(0xFFF5F5F5)),
                                      borderRadius: BorderRadius.circular(8),
                                    ),
                                    alignment: Alignment.center,
                                    child: Text(
                                      sector.name,
                                      style: TextStyle(
                                        fontSize: 12,
                                        fontWeight: FontWeight.w700,
                                        color: isSelected
                                            ? Colors.white
                                            : (isTaken
                                                  ? tertiaryText.withValues(
                                                      alpha: 0.6,
                                                    )
                                                  : null),
                                      ),
                                    ),
                                  ),
                                );
                              },
                            ),
                          const SizedBox(height: 16),
                          Row(
                            children: [
                              _LegendDot(
                                color: isDark
                                    ? AppColors.darkSurfaceMuted
                                    : const Color(0xFFF5F5F5),
                                label: 'Slobodno',
                              ),
                              const SizedBox(width: 14),
                              const _LegendDot(
                                color: AppColors.primary,
                                label: 'Odabrano',
                              ),
                            ],
                          ),
                          if (selectedSector != null) ...[
                            const SizedBox(height: 24),
                            const Text(
                              'Pretplata',
                              style: TextStyle(
                                fontSize: 15,
                                fontWeight: FontWeight.w700,
                              ),
                            ),
                            const SizedBox(height: 10),
                            Container(
                              padding: const EdgeInsets.all(14),
                              decoration: BoxDecoration(
                                border: Border.all(
                                  color: Theme.of(context).colorScheme.primary,
                                  width: 2,
                                ),
                                borderRadius: BorderRadius.circular(12),
                                color: isDark
                                    ? AppColors.darkSurfaceMuted
                                    : AppColors.lightSurfaceTint,
                              ),
                              child: Row(
                                mainAxisAlignment:
                                    MainAxisAlignment.spaceBetween,
                                children: [
                                  Column(
                                    crossAxisAlignment:
                                        CrossAxisAlignment.start,
                                    children: [
                                      const Text(
                                        'Mjesečna pretplata',
                                        style: TextStyle(
                                          fontSize: 14,
                                          fontWeight: FontWeight.w700,
                                        ),
                                      ),
                                      Text(
                                        'Mjesto ${selectedSector.name} · Neograničen ulaz/izlaz',
                                        style: TextStyle(
                                          fontSize: 12,
                                          color: tertiaryText,
                                        ),
                                      ),
                                    ],
                                  ),
                                  Text(
                                    '${selectedSector.price.toStringAsFixed(0)} KM',
                                    style: TextStyle(
                                      fontSize: 15,
                                      fontWeight: FontWeight.w700,
                                      color: Theme.of(
                                        context,
                                      ).colorScheme.primary,
                                    ),
                                  ),
                                ],
                              ),
                            ),
                          ],
                          if (_submitError != null) ...[
                            const SizedBox(height: 12),
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
                  ],
                ),
              ),
            ),
          ),
          if (selectedSector != null)
            Positioned(
              left: 0,
              right: 0,
              bottom: 0,
              child: PurchaseBottomBar(
                totalLabel: 'Mjesto ${selectedSector.name}',
                total: selectedSector.price,
                buttonLabel: 'Kupi ulaznice',
                enabled: !_isSubmitting,
                isLoading: _isSubmitting,
                onPressed: _goToPayment,
              ),
            ),
        ],
      ),
    );
  }
}

class _LegendDot extends StatelessWidget {
  final Color color;
  final String label;

  const _LegendDot({required this.color, required this.label});

  @override
  Widget build(BuildContext context) {
    final isDark = Theme.of(context).brightness == Brightness.dark;
    final tertiaryText = isDark
        ? AppColors.darkTextTertiary
        : AppColors.lightTextTertiary;
    return Row(
      mainAxisSize: MainAxisSize.min,
      children: [
        Container(
          width: 10,
          height: 10,
          decoration: BoxDecoration(
            color: color,
            borderRadius: BorderRadius.circular(3),
          ),
        ),
        const SizedBox(width: 6),
        Text(label, style: TextStyle(fontSize: 12, color: tertiaryText)),
      ],
    );
  }
}
