import 'package:flutter/material.dart';

import '../models/responses/validation_product_response.dart';
import '../models/ticketing_mode.dart';
import '../services/api_exception.dart';
import '../services/validation_service.dart';
import '../theme/app_colors.dart';
import '../widgets/responsive_page.dart';
import 'scan_ticket_screen.dart';

/// "Validacija" tab body — the organizer's list of what they're checking
/// people into **today**, and the entry point to the scanner.
///
/// Deliberately a list of products rather than a bare "scan anything" button:
/// validation is always against one specific product (a ticket to last night's
/// concert is a perfectly good ticket and still must not open today's gate),
/// so the organizer picks the gate they're standing at first.
///
/// No `Scaffold`/`AppBar` of its own — [MainShell] supplies the plain title
/// header, matching how Moje ulaznice is embedded.
class ValidationProductsScreen extends StatefulWidget {
  const ValidationProductsScreen({super.key});

  @override
  State<ValidationProductsScreen> createState() => _ValidationProductsScreenState();
}

class _ValidationProductsScreenState extends State<ValidationProductsScreen> {
  final _validationService = ValidationService();

  bool _isLoading = true;
  String? _errorMessage;
  List<ValidationProductResponse> _products = [];

  @override
  void initState() {
    super.initState();
    _load();
  }

  Future<void> _load() async {
    setState(() {
      _isLoading = true;
      _errorMessage = null;
    });
    try {
      final products = await _validationService.getProductsForToday();
      if (!mounted) return;
      setState(() {
        _products = products;
        _isLoading = false;
      });
    } on ApiException catch (e) {
      if (!mounted) return;
      setState(() {
        _errorMessage = e.apiError.displayMessage;
        _isLoading = false;
      });
    } catch (_) {
      if (!mounted) return;
      setState(() {
        _errorMessage = 'Listu nije moguće učitati. Pokušajte ponovo.';
        _isLoading = false;
      });
    }
  }

  Future<void> _openScanner(ValidationProductResponse product) async {
    await Navigator.of(context).push(
      MaterialPageRoute(builder: (_) => ScanTicketScreen(product: product)),
    );
    // Counts move while the scanner is open, so refresh on the way back rather
    // than showing the organizer a stale "validirano 0 / 40".
    if (mounted) await _load();
  }

  @override
  Widget build(BuildContext context) {
    final isDark = Theme.of(context).brightness == Brightness.dark;
    final tertiaryText = isDark ? AppColors.darkTextTertiary : AppColors.lightTextTertiary;

    if (_isLoading) {
      return const Center(child: CircularProgressIndicator());
    }

    if (_errorMessage != null) {
      return Center(
        child: Padding(
          padding: const EdgeInsets.all(32),
          child: Column(
            mainAxisSize: MainAxisSize.min,
            children: [
              Text(_errorMessage!, textAlign: TextAlign.center, style: TextStyle(color: tertiaryText)),
              const SizedBox(height: 12),
              OutlinedButton(onPressed: _load, child: const Text('Pokušaj ponovo')),
            ],
          ),
        ),
      );
    }

    return ResponsivePage(
      padding: EdgeInsets.zero,
      child: RefreshIndicator(
        onRefresh: _load,
        child: _products.isEmpty
            // Still a scrollable, so pull-to-refresh works on the empty state
            // too — otherwise an organizer whose event starts in an hour has no
            // way to recheck without leaving the tab.
            ? ListView(
                padding: const EdgeInsets.fromLTRB(32, 80, 32, 32),
                children: [
                  Icon(Icons.qr_code_scanner_rounded, size: 48, color: tertiaryText),
                  const SizedBox(height: 16),
                  Text(
                    'Danas nema događaja za validaciju.',
                    textAlign: TextAlign.center,
                    style: TextStyle(color: tertiaryText),
                  ),
                ],
              )
            : ListView.separated(
                padding: const EdgeInsets.fromLTRB(20, 16, 20, 16),
                itemCount: _products.length,
                separatorBuilder: (_, _) => const SizedBox(height: 12),
                itemBuilder: (context, index) => _ValidationProductCard(
                  product: _products[index],
                  onTap: () => _openScanner(_products[index]),
                ),
              ),
      ),
    );
  }
}

class _ValidationProductCard extends StatelessWidget {
  final ValidationProductResponse product;
  final VoidCallback onTap;

  const _ValidationProductCard({required this.product, required this.onTap});

  String get _subtitle {
    final mode = switch (product.ticketingMode) {
      TicketingMode.dailyEntry => 'Dnevne ulaznice',
      TicketingMode.recurringReservation => 'Rezervacije',
      TicketingMode.singleOccurrence => 'Događaj',
    };
    if (product.date == null) return mode;

    final hh = product.date!.hour.toString().padLeft(2, '0');
    final mm = product.date!.minute.toString().padLeft(2, '0');
    return '$mode · $hh:$mm h';
  }

  @override
  Widget build(BuildContext context) {
    final isDark = Theme.of(context).brightness == Brightness.dark;
    final tertiaryText = isDark ? AppColors.darkTextTertiary : AppColors.lightTextTertiary;
    final primary = Theme.of(context).colorScheme.primary;

    return InkWell(
      borderRadius: BorderRadius.circular(16),
      onTap: onTap,
      child: Card(
        margin: EdgeInsets.zero,
        child: Padding(
          padding: const EdgeInsets.all(16),
          child: Row(
            children: [
              Expanded(
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  mainAxisSize: MainAxisSize.min,
                  children: [
                    Text(
                      product.name,
                      style: const TextStyle(fontSize: 15, fontWeight: FontWeight.w700),
                      maxLines: 2,
                      overflow: TextOverflow.ellipsis,
                    ),
                    const SizedBox(height: 4),
                    Text(_subtitle, style: TextStyle(fontSize: 12, color: tertiaryText)),
                    const SizedBox(height: 10),
                    Text(
                      'Validirano ${product.validatedToday} / ${product.totalToday}',
                      style: TextStyle(fontSize: 12, fontWeight: FontWeight.w600, color: primary),
                    ),
                    const SizedBox(height: 6),
                    ClipRRect(
                      borderRadius: BorderRadius.circular(999),
                      child: LinearProgressIndicator(
                        // totalToday can be 0 only if the row wouldn't have been
                        // returned at all, but guard anyway rather than divide by zero.
                        value: product.totalToday == 0
                            ? 0
                            : product.validatedToday / product.totalToday,
                        minHeight: 5,
                        backgroundColor: isDark ? AppColors.darkSurfaceMuted : const Color(0xFFEEEEEE),
                      ),
                    ),
                  ],
                ),
              ),
              const SizedBox(width: 12),
              Icon(Icons.qr_code_scanner_rounded, size: 26, color: primary),
            ],
          ),
        ),
      ),
    );
  }
}
