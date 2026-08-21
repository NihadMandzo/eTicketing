import 'package:flutter/material.dart';

import '../models/responses/product_response.dart';
import '../models/responses/ticket_response.dart';
import '../services/api_exception.dart';
import '../services/catalog_service.dart';
import '../services/purchase_service.dart';
import '../theme/app_colors.dart';
import '../widgets/responsive_page.dart';
import 'ticket_qr_screen.dart';

const _ticketsPageSize = 20;

/// Ticketing-mode filter, mirrors web's `TicketFilter` in
/// `profile.component.ts` — kept in sync so both platforms offer the exact
/// same filtering capability over "moje ulaznice".
enum _TicketTypeFilter {
  sve,
  singleOccurrence,
  dailyEntry,
  recurringReservation,
}

/// "Moje ulaznice" content — mockup screens 5/8: Nadolazeće/Iskorištene
/// segmented tabs + a type-filter chip row (Sve/Događaji/Dnevne
/// ulaznice/Rezervacije, mirroring web's Profile ticket tab) +
/// timeline-style ticket-stub cards. No `Scaffold`/`AppBar` of its own so it
/// can be embedded either as [MainShell]'s tab body (plain title header
/// supplied by the caller) or pushed from `ProfileScreen`'s "Historija
/// narudžbi" row inside a real `Scaffold`+`AppBar`.
///
/// `TicketResponse` has no `ProductName`/`ProductDate` of its own (only
/// `SectorName`/`ValidDate`/`ValidFrom`/`ValidTo`) — SingleOccurrence
/// tickets in particular carry no per-ticket date at all, since the single
/// showing date lives on `Product.Date`. This screen batch-fetches each
/// distinct Product referenced by the caller's tickets (small N in
/// practice) to show a real event title and to derive an "upcoming vs.
/// past" split for SingleOccurrence tickets from `Product.Date`.
class MyTicketsScreen extends StatefulWidget {
  const MyTicketsScreen({super.key});

  @override
  State<MyTicketsScreen> createState() => _MyTicketsScreenState();
}

class _MyTicketsScreenState extends State<MyTicketsScreen> {
  final _purchaseService = PurchaseService();
  final _catalogService = CatalogService();

  bool _isLoading = true;
  bool _isLoadingMore = false;
  String? _errorMessage;
  List<TicketResponse> _tickets = [];
  Map<String, ProductResponse> _productsById = {};
  int _page = 0;
  int _totalCount = 0;
  bool _showUpcoming = true;
  _TicketTypeFilter _typeFilter = _TicketTypeFilter.sve;

  bool get _hasMore => _tickets.length < _totalCount;

  // Ticket doesn't carry TicketingMode directly (denormalized only as far as
  // SectorId/ProductId) — SingleOccurrence tickets have no
  // ValidDate/ValidFrom, DailyEntry has ValidDate, RecurringReservation has
  // ValidFrom. Mirrors web's `modeOf()` in profile.component.ts exactly.
  _TicketTypeFilter _modeOf(TicketResponse ticket) {
    if (ticket.validDate != null) return _TicketTypeFilter.dailyEntry;
    if (ticket.validFrom != null) return _TicketTypeFilter.recurringReservation;
    return _TicketTypeFilter.singleOccurrence;
  }

  @override
  void initState() {
    super.initState();
    _loadPage(0);
  }

  Future<void> _load() => _loadPage(0);

  /// Appends page [page]'s tickets onto `_tickets` — a running list across every page fetched so
  /// far, not just the current page, so "Prikaži još" never throws away what's already on screen.
  Future<void> _loadPage(int page) async {
    setState(() {
      if (page == 0) {
        _isLoading = true;
      } else {
        _isLoadingMore = true;
      }
      _errorMessage = null;
    });
    try {
      final result = await _purchaseService.getMyTickets(
        page: page,
        pageSize: _ticketsPageSize,
      );

      // Only fetch products this page actually introduced — _productsById already carries
      // whatever earlier pages resolved, so re-fetching those every "Prikaži još" click would be
      // wasted requests for products already known.
      final newProductIds = result.items
          .map((t) => t.productId)
          .where((id) => !_productsById.containsKey(id))
          .toSet();
      final newProducts = await Future.wait(
        newProductIds.map((id) async {
          try {
            return await _catalogService.getProductById(id);
          } catch (_) {
            return null;
          }
        }),
      );
      if (!mounted) return;
      setState(() {
        _tickets = page == 0 ? result.items : [..._tickets, ...result.items];
        _productsById = {
          ..._productsById,
          for (final p in newProducts)
            if (p != null) p.id: p,
        };
        _page = page;
        _totalCount = result.totalCount;
        _isLoading = false;
        _isLoadingMore = false;
      });
    } on ApiException catch (e) {
      if (!mounted) return;
      setState(() {
        _errorMessage = e.apiError.displayMessage;
        _isLoading = false;
        _isLoadingMore = false;
      });
    } catch (_) {
      if (!mounted) return;
      setState(() {
        _errorMessage = 'Ulaznice nije moguće učitati. Pokušajte ponovo.';
        _isLoading = false;
        _isLoadingMore = false;
      });
    }
  }

  void _loadMore() {
    if (_isLoadingMore || !_hasMore) return;
    _loadPage(_page + 1);
  }

  DateTime? _effectiveDate(TicketResponse ticket) {
    if (ticket.validTo != null) return ticket.validTo;
    if (ticket.validDate != null) return ticket.validDate;
    return _productsById[ticket.productId]?.date;
  }

  bool _isUpcoming(TicketResponse ticket) {
    final date = _effectiveDate(ticket);
    if (date == null) return true;
    final today = DateTime.now();
    return !date.isBefore(DateTime(today.year, today.month, today.day));
  }

  @override
  Widget build(BuildContext context) {
    final isDark = Theme.of(context).brightness == Brightness.dark;
    final tertiaryText = isDark
        ? AppColors.darkTextTertiary
        : AppColors.lightTextTertiary;

    final filtered =
        _tickets
            .where((t) => _isUpcoming(t) == _showUpcoming)
            .where(
              (t) =>
                  _typeFilter == _TicketTypeFilter.sve ||
                  _modeOf(t) == _typeFilter,
            )
            .toList()
          ..sort(
            (a, b) => (_effectiveDate(a) ?? a.createdAt).compareTo(
              _effectiveDate(b) ?? b.createdAt,
            ),
          );

    return ResponsivePage(
      padding: EdgeInsets.zero,
      child: Column(
        children: [
          Padding(
            padding: const EdgeInsets.fromLTRB(20, 0, 20, 0),
            child: Row(
              children: [
                _TabLabel(
                  label: 'Nadolazeće',
                  selected: _showUpcoming,
                  onTap: () => setState(() => _showUpcoming = true),
                ),
                const SizedBox(width: 20),
                _TabLabel(
                  label: 'Iskorištene',
                  selected: !_showUpcoming,
                  onTap: () => setState(() => _showUpcoming = false),
                ),
              ],
            ),
          ),
          Container(
            height: 1,
            color: isDark ? AppColors.darkBorder : AppColors.lightBorder,
          ),
          const SizedBox(height: 12),
          SizedBox(
            height: 32,
            child: ListView(
              scrollDirection: Axis.horizontal,
              padding: const EdgeInsets.symmetric(horizontal: 20),
              children: [
                _TypeFilterChip(
                  label: 'Sve',
                  selected: _typeFilter == _TicketTypeFilter.sve,
                  onTap: () =>
                      setState(() => _typeFilter = _TicketTypeFilter.sve),
                ),
                const SizedBox(width: 8),
                _TypeFilterChip(
                  label: 'Događaji',
                  selected: _typeFilter == _TicketTypeFilter.singleOccurrence,
                  onTap: () => setState(
                    () => _typeFilter = _TicketTypeFilter.singleOccurrence,
                  ),
                ),
                const SizedBox(width: 8),
                _TypeFilterChip(
                  label: 'Dnevne ulaznice',
                  selected: _typeFilter == _TicketTypeFilter.dailyEntry,
                  onTap: () => setState(
                    () => _typeFilter = _TicketTypeFilter.dailyEntry,
                  ),
                ),
                const SizedBox(width: 8),
                _TypeFilterChip(
                  label: 'Rezervacije',
                  selected:
                      _typeFilter == _TicketTypeFilter.recurringReservation,
                  onTap: () => setState(
                    () => _typeFilter = _TicketTypeFilter.recurringReservation,
                  ),
                ),
              ],
            ),
          ),
          const SizedBox(height: 4),
          Expanded(
            child: _isLoading
                ? const Center(child: CircularProgressIndicator())
                : _errorMessage != null
                ? Center(
                    child: Padding(
                      padding: const EdgeInsets.all(32),
                      child: Column(
                        mainAxisSize: MainAxisSize.min,
                        children: [
                          Text(
                            _errorMessage!,
                            textAlign: TextAlign.center,
                            style: TextStyle(color: tertiaryText),
                          ),
                          const SizedBox(height: 12),
                          OutlinedButton(
                            onPressed: _load,
                            child: const Text('Pokušaj ponovo'),
                          ),
                        ],
                      ),
                    ),
                  )
                : filtered.isEmpty
                ? Center(
                    child: Text(
                      _showUpcoming
                          ? 'Nemate nadolazećih ulaznica.'
                          : 'Nemate iskorištenih ulaznica.',
                      style: TextStyle(color: tertiaryText),
                    ),
                  )
                : RefreshIndicator(
                    onRefresh: _load,
                    child: ListView.separated(
                      padding: const EdgeInsets.fromLTRB(20, 16, 20, 16),
                      itemCount: filtered.length + (_hasMore ? 1 : 0),
                      separatorBuilder: (_, _) => const SizedBox(height: 16),
                      itemBuilder: (context, index) {
                        if (index >= filtered.length) {
                          return Center(
                            child: Padding(
                              padding: const EdgeInsets.only(top: 4),
                              child: _isLoadingMore
                                  ? const SizedBox(
                                      width: 20,
                                      height: 20,
                                      child: CircularProgressIndicator(
                                        strokeWidth: 2,
                                      ),
                                    )
                                  : OutlinedButton(
                                      onPressed: _loadMore,
                                      child: const Text('Prikaži još'),
                                    ),
                            ),
                          );
                        }
                        final ticket = filtered[index];
                        final product = _productsById[ticket.productId];
                        return _TicketStubCard(
                          ticket: ticket,
                          productName: product?.name ?? ticket.sectorName,
                          onTap: () => Navigator.of(context).push(
                            MaterialPageRoute(
                              builder: (_) => TicketQrScreen(
                                ticket: ticket,
                                productName: product?.name ?? ticket.sectorName,
                              ),
                            ),
                          ),
                        );
                      },
                    ),
                  ),
          ),
        ],
      ),
    );
  }
}

class _TypeFilterChip extends StatelessWidget {
  final String label;
  final bool selected;
  final VoidCallback onTap;

  const _TypeFilterChip({
    required this.label,
    required this.selected,
    required this.onTap,
  });

  @override
  Widget build(BuildContext context) {
    final isDark = Theme.of(context).brightness == Brightness.dark;
    final primary = Theme.of(context).colorScheme.primary;
    return InkWell(
      borderRadius: BorderRadius.circular(999),
      onTap: onTap,
      child: Container(
        padding: const EdgeInsets.symmetric(horizontal: 14, vertical: 6),
        decoration: BoxDecoration(
          color: selected
              ? primary
              : (isDark ? AppColors.darkSurfaceMuted : const Color(0xFFF5F5F5)),
          borderRadius: BorderRadius.circular(999),
        ),
        child: Text(
          label,
          style: TextStyle(
            fontSize: 12,
            fontWeight: FontWeight.w600,
            color: selected
                ? Colors.white
                : (isDark
                      ? AppColors.darkTextPrimary
                      : AppColors.lightTextPrimary),
          ),
        ),
      ),
    );
  }
}

class _TabLabel extends StatelessWidget {
  final String label;
  final bool selected;
  final VoidCallback onTap;

  const _TabLabel({
    required this.label,
    required this.selected,
    required this.onTap,
  });

  @override
  Widget build(BuildContext context) {
    final isDark = Theme.of(context).brightness == Brightness.dark;
    final primary = Theme.of(context).colorScheme.primary;
    final tertiaryText = isDark
        ? AppColors.darkTextTertiary
        : AppColors.lightTextTertiary;
    return InkWell(
      onTap: onTap,
      child: Container(
        padding: const EdgeInsets.only(bottom: 10),
        decoration: BoxDecoration(
          border: Border(
            bottom: BorderSide(
              color: selected ? primary : Colors.transparent,
              width: 2,
            ),
          ),
        ),
        child: Text(
          label,
          style: TextStyle(
            fontSize: 14,
            fontWeight: FontWeight.w700,
            color: selected ? primary : tertiaryText,
          ),
        ),
      ),
    );
  }
}

class _TicketStubCard extends StatelessWidget {
  final TicketResponse ticket;
  final String productName;
  final VoidCallback onTap;

  const _TicketStubCard({
    required this.ticket,
    required this.productName,
    required this.onTap,
  });

  DateTime get _headlineDate =>
      ticket.validDate ?? ticket.validFrom ?? ticket.createdAt;

  static const _months = [
    'JAN',
    'FEB',
    'MAR',
    'APR',
    'MAJ',
    'JUN',
    'JUL',
    'AVG',
    'SEP',
    'OKT',
    'NOV',
    'DEC',
  ];

  @override
  Widget build(BuildContext context) {
    final isDark = Theme.of(context).brightness == Brightness.dark;
    final tertiaryText = isDark
        ? AppColors.darkTextTertiary
        : AppColors.lightTextTertiary;
    final date = _headlineDate;

    return InkWell(
      borderRadius: BorderRadius.circular(16),
      onTap: onTap,
      child: Card(
        clipBehavior: Clip.antiAlias,
        margin: EdgeInsets.zero,
        child: SizedBox(
          height: 104,
          child: Row(
            children: [
              Container(
                width: 96,
                decoration: const BoxDecoration(
                  gradient: LinearGradient(
                    colors: [AppColors.primary, AppColors.secondary],
                  ),
                ),
                child: Column(
                  mainAxisAlignment: MainAxisAlignment.center,
                  children: [
                    Text(
                      _months[date.month - 1],
                      style: const TextStyle(
                        fontSize: 11,
                        color: Colors.white70,
                      ),
                    ),
                    Text(
                      '${date.day}',
                      style: const TextStyle(
                        fontSize: 22,
                        fontWeight: FontWeight.w700,
                        color: Colors.white,
                      ),
                    ),
                  ],
                ),
              ),
              Expanded(
                child: Padding(
                  padding: const EdgeInsets.symmetric(horizontal: 16),
                  child: Column(
                    crossAxisAlignment: CrossAxisAlignment.start,
                    mainAxisSize: MainAxisSize.min,
                    children: [
                      Text(
                        productName,
                        style: const TextStyle(
                          fontSize: 15,
                          fontWeight: FontWeight.w700,
                        ),
                        maxLines: 1,
                        overflow: TextOverflow.ellipsis,
                      ),
                      const SizedBox(height: 4),
                      Text(
                        ticket.ticketTypeName != null
                            ? '${ticket.sectorName} · ${ticket.ticketTypeName}'
                            : ticket.sectorName,
                        style: TextStyle(fontSize: 12, color: tertiaryText),
                        maxLines: 1,
                        overflow: TextOverflow.ellipsis,
                      ),
                    ],
                  ),
                ),
              ),
              Padding(
                padding: const EdgeInsets.only(right: 14),
                child: Icon(
                  Icons.chevron_right_rounded,
                  size: 18,
                  color: tertiaryText,
                ),
              ),
            ],
          ),
        ),
      ),
    );
  }
}
