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

/// The three kinds of thing this platform sells, which is also how this screen is organised.
///
/// One tab per `Category.TicketingMode` and no "Sve": a concert seat, a museum day pass and a
/// monthly parking space have different dates, different validity and different questions attached
/// to them, so a single merged list sorted by date read as three unrelated things interleaved. The
/// tab you are on is the question you are asking.
enum _TicketKind {
  event(label: 'Događaji'),
  daily(label: 'Dnevne'),
  subscription(label: 'Pretplate');

  const _TicketKind({required this.label});

  final String label;
}

/// "Moje ulaznice" — the buyer's own tickets, in three tabs by kind and grouped under the event
/// they belong to.
///
/// **Grouping is the point.** Four tickets to the same concert are one purchase and one plan for
/// one evening; as four peer rows in a flat list they read as four separate things to keep track
/// of. The event name is stated once, as a heading, and the stubs beneath it carry only what
/// differs between them (sector, ticket type).
///
/// `TicketResponse` has no `ProductName`/`ProductDate` of its own (only `SectorName`/`ValidDate`/
/// `ValidFrom`/`ValidTo`) — SingleOccurrence tickets in particular carry no per-ticket date at
/// all, since the single showing date lives on `Product.Date`. This screen batch-fetches each
/// distinct Product referenced by the caller's tickets (small N in practice) to title each group
/// and to derive the upcoming/past split for SingleOccurrence tickets from `Product.Date`.
///
/// **A deleted event has no tickets.** When the product behind a ticket comes back 404 the event
/// was cancelled and removed, the buyer was told so by email, and the ticket admits them to
/// nothing — so it is dropped rather than shown under a heading this app would have to invent.
/// Any other lookup failure (network, a 500) is treated as temporary and the ticket stays, grouped
/// under its sector name, because hiding someone's ticket over a flaky request would be worse than
/// a plain heading.
///
/// No `Scaffold`/`AppBar` of its own so it can be embedded as [MainShell]'s tab body.
class MyTicketsScreen extends StatefulWidget {
  const MyTicketsScreen({super.key});

  @override
  State<MyTicketsScreen> createState() => _MyTicketsScreenState();
}

/// One event's worth of tickets, in the order they should be shown.
class _TicketGroup {
  final String title;
  final String? subtitle;
  final List<TicketResponse> tickets;

  const _TicketGroup({required this.title, this.subtitle, required this.tickets});
}

class _MyTicketsScreenState extends State<MyTicketsScreen> {
  final _purchaseService = PurchaseService();
  final _catalogService = CatalogService();

  bool _isLoading = true;
  bool _isLoadingMore = false;
  String? _errorMessage;
  List<TicketResponse> _tickets = [];
  final Map<String, ProductResponse> _productsById = {};

  /// Products the Catalog answered 404 for — the event was deleted, so its tickets are gone too.
  final Set<String> _deletedProductIds = {};

  int _page = 0;
  int _totalCount = 0;
  bool _showUpcoming = true;
  _TicketKind _kind = _TicketKind.event;

  bool get _hasMore => _tickets.length < _totalCount;

  // Ticket doesn't carry TicketingMode directly (denormalized only as far as
  // SectorId/ProductId) — SingleOccurrence tickets have no ValidDate/ValidFrom, DailyEntry has
  // ValidDate, RecurringReservation has ValidFrom. Mirrors web's `modeOf()` in
  // profile.component.ts exactly.
  _TicketKind _kindOf(TicketResponse ticket) {
    if (ticket.validDate != null) return _TicketKind.daily;
    if (ticket.validFrom != null) return _TicketKind.subscription;
    return _TicketKind.event;
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
          .where((id) => !_productsById.containsKey(id) && !_deletedProductIds.contains(id))
          .toSet();
      final lookups = await Future.wait(newProductIds.map(_resolveProduct));

      if (!mounted) return;
      setState(() {
        _tickets = page == 0 ? result.items : [..._tickets, ...result.items];
        for (final lookup in lookups) {
          if (lookup.product != null) {
            _productsById[lookup.product!.id] = lookup.product!;
          } else if (lookup.isDeleted) {
            _deletedProductIds.add(lookup.productId);
          }
        }
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

  /// Looks one product up, separating "this event is gone" (404) from "the request didn't work"
  /// (anything else) — only the first is a reason to stop showing someone their ticket.
  Future<_ProductLookup> _resolveProduct(String productId) async {
    try {
      return _ProductLookup(productId: productId, product: await _catalogService.getProductById(productId));
    } on ApiException catch (e) {
      return _ProductLookup(productId: productId, isDeleted: e.statusCode == 404);
    } catch (_) {
      return _ProductLookup(productId: productId);
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

  /// The visible tickets, gathered under the event each belongs to.
  ///
  /// Groups are ordered by their soonest ticket, and tickets within a group by date too, so the
  /// next thing the buyer has to turn up to is always at the top of the screen.
  List<_TicketGroup> get _groups {
    final visible = _tickets
        .where((t) => !_deletedProductIds.contains(t.productId))
        .where((t) => _kindOf(t) == _kind)
        .where((t) => _isUpcoming(t) == _showUpcoming)
        .toList();

    final byProduct = <String, List<TicketResponse>>{};
    for (final ticket in visible) {
      byProduct.putIfAbsent(ticket.productId, () => []).add(ticket);
    }

    final groups = byProduct.entries.map((entry) {
      final tickets = entry.value
        ..sort((a, b) => (_effectiveDate(a) ?? a.createdAt).compareTo(_effectiveDate(b) ?? b.createdAt));
      final product = _productsById[entry.key];
      return _TicketGroup(
        title: product?.name ?? tickets.first.sectorName,
        subtitle: _groupSubtitle(product, tickets),
        tickets: tickets,
      );
    }).toList();

    groups.sort((a, b) {
      final aDate = _effectiveDate(a.tickets.first) ?? a.tickets.first.createdAt;
      final bDate = _effectiveDate(b.tickets.first) ?? b.tickets.first.createdAt;
      return aDate.compareTo(bDate);
    });
    return groups;
  }

  /// The one line under an event's name: when it is, and how many tickets are held for it.
  String? _groupSubtitle(ProductResponse? product, List<TicketResponse> tickets) {
    final count = tickets.length;
    final countLabel = switch (count) {
      1 => '1 ulaznica',
      2 || 3 || 4 => '$count ulaznice',
      _ => '$count ulaznica',
    };

    final date = switch (_kind) {
      _TicketKind.event => product?.date,
      // A day pass or a subscription period belongs to the ticket, not to the product — the
      // product has no date at all in those two modes.
      _ => null,
    };
    return date == null ? countLabel : '${_formatDate(date)} · $countLabel';
  }

  static const _monthNames = [
    'januar', 'februar', 'mart', 'april', 'maj', 'juni',
    'juli', 'august', 'septembar', 'oktobar', 'novembar', 'decembar',
  ];

  static String _formatDate(DateTime date) => '${date.day}. ${_monthNames[date.month - 1]} ${date.year}';

  String get _emptyMessage => switch ((_kind, _showUpcoming)) {
    (_TicketKind.event, true) => 'Nemate nadolazećih ulaznica za događaje.',
    (_TicketKind.event, false) => 'Nemate prošlih ulaznica za događaje.',
    (_TicketKind.daily, true) => 'Nemate važećih dnevnih ulaznica.',
    (_TicketKind.daily, false) => 'Nemate prošlih dnevnih ulaznica.',
    (_TicketKind.subscription, true) => 'Nemate aktivnih pretplata.',
    (_TicketKind.subscription, false) => 'Nemate isteklih pretplata.',
  };

  @override
  Widget build(BuildContext context) {
    final isDark = Theme.of(context).brightness == Brightness.dark;
    final tertiaryText = isDark ? AppColors.darkTextTertiary : AppColors.lightTextTertiary;
    final groups = _groups;

    return ResponsivePage(
      padding: EdgeInsets.zero,
      child: Column(
        children: [
          Padding(
            padding: const EdgeInsets.fromLTRB(20, 0, 20, 0),
            child: Row(
              children: [
                for (final kind in _TicketKind.values) ...[
                  _TabLabel(
                    label: kind.label,
                    selected: _kind == kind,
                    onTap: () => setState(() => _kind = kind),
                  ),
                  if (kind != _TicketKind.values.last) const SizedBox(width: 20),
                ],
              ],
            ),
          ),
          Container(height: 1, color: isDark ? AppColors.darkBorder : AppColors.lightBorder),
          const SizedBox(height: 12),
          Padding(
            padding: const EdgeInsets.symmetric(horizontal: 20),
            child: Row(
              children: [
                _PeriodChip(
                  label: 'Nadolazeće',
                  selected: _showUpcoming,
                  onTap: () => setState(() => _showUpcoming = true),
                ),
                const SizedBox(width: 8),
                _PeriodChip(
                  // "Prošle", not "Iskorištene": this split is by date, not by whether the ticket
                  // was ever scanned at the gate, and the old label promised the second.
                  label: 'Prošle',
                  selected: !_showUpcoming,
                  onTap: () => setState(() => _showUpcoming = false),
                ),
              ],
            ),
          ),
          const SizedBox(height: 4),
          Expanded(child: _buildBody(groups, tertiaryText)),
        ],
      ),
    );
  }

  Widget _buildBody(List<_TicketGroup> groups, Color tertiaryText) {
    if (_isLoading) return const Center(child: CircularProgressIndicator());

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

    if (groups.isEmpty) {
      return RefreshIndicator(
        onRefresh: _load,
        child: ListView(
          physics: const AlwaysScrollableScrollPhysics(),
          children: [
            Padding(
              padding: const EdgeInsets.fromLTRB(32, 64, 32, 32),
              child: Column(
                children: [
                  Icon(Icons.confirmation_number_outlined, size: 32, color: tertiaryText),
                  const SizedBox(height: 12),
                  Text(_emptyMessage, textAlign: TextAlign.center, style: TextStyle(color: tertiaryText)),
                ],
              ),
            ),
          ],
        ),
      );
    }

    return RefreshIndicator(
      onRefresh: _load,
      child: ListView.builder(
        padding: const EdgeInsets.fromLTRB(20, 16, 20, 24),
        itemCount: groups.length + (_hasMore ? 1 : 0),
        itemBuilder: (context, index) {
          if (index >= groups.length) {
            return Padding(
              padding: const EdgeInsets.only(top: 8),
              child: Center(
                child: _isLoadingMore
                    ? const SizedBox(width: 20, height: 20, child: CircularProgressIndicator(strokeWidth: 2))
                    : OutlinedButton(onPressed: _loadMore, child: const Text('Prikaži još')),
              ),
            );
          }
          return _EventGroup(
            group: groups[index],
            isLast: index == groups.length - 1,
            onTicketTap: (ticket) => Navigator.of(context).push(
              MaterialPageRoute(
                builder: (_) => TicketQrScreen(ticket: ticket, productName: groups[index].title),
              ),
            ),
          );
        },
      ),
    );
  }
}

/// One product lookup's outcome — the product, or why there isn't one.
class _ProductLookup {
  final String productId;
  final ProductResponse? product;

  /// The Catalog answered 404: the event was deleted, not merely unreachable.
  final bool isDeleted;

  const _ProductLookup({required this.productId, this.product, this.isDeleted = false});
}

/// An event's heading plus its stubs.
///
/// The heading carries a short brand-coloured rule on its left. That rule is the only thing
/// binding the stubs below it to the name above them, which is why it is there and why it is the
/// one piece of colour in an otherwise quiet list.
class _EventGroup extends StatelessWidget {
  final _TicketGroup group;
  final bool isLast;
  final void Function(TicketResponse ticket) onTicketTap;

  const _EventGroup({required this.group, required this.isLast, required this.onTicketTap});

  @override
  Widget build(BuildContext context) {
    final isDark = Theme.of(context).brightness == Brightness.dark;
    final tertiaryText = isDark ? AppColors.darkTextTertiary : AppColors.lightTextTertiary;
    final primary = Theme.of(context).colorScheme.primary;

    return Padding(
      padding: EdgeInsets.only(bottom: isLast ? 0 : 28),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: [
          Row(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              Container(
                width: 3,
                height: 30,
                margin: const EdgeInsets.only(top: 2, right: 10),
                decoration: BoxDecoration(color: primary, borderRadius: BorderRadius.circular(2)),
              ),
              Expanded(
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    Text(
                      group.title,
                      style: const TextStyle(fontSize: 16, fontWeight: FontWeight.w700, height: 1.2),
                      maxLines: 2,
                      overflow: TextOverflow.ellipsis,
                    ),
                    if (group.subtitle != null) ...[
                      const SizedBox(height: 2),
                      Text(group.subtitle!, style: TextStyle(fontSize: 12, color: tertiaryText)),
                    ],
                  ],
                ),
              ),
            ],
          ),
          const SizedBox(height: 12),
          for (final ticket in group.tickets)
            Padding(
              padding: const EdgeInsets.only(bottom: 10),
              child: _TicketStubCard(ticket: ticket, onTap: () => onTicketTap(ticket)),
            ),
        ],
      ),
    );
  }
}

/// Nadolazeće / Prošle. A pill rather than a second row of underlined tabs, so it reads as a
/// filter applied to the tab above it and not as a competing tab set.
class _PeriodChip extends StatelessWidget {
  final String label;
  final bool selected;
  final VoidCallback onTap;

  const _PeriodChip({required this.label, required this.selected, required this.onTap});

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
          color: selected ? primary : (isDark ? AppColors.darkSurfaceMuted : AppColors.lightSurfaceMuted),
          borderRadius: BorderRadius.circular(999),
        ),
        child: Text(
          label,
          style: TextStyle(
            fontSize: 12,
            fontWeight: FontWeight.w600,
            color: selected
                ? Colors.white
                : (isDark ? AppColors.darkTextPrimary : AppColors.lightTextPrimary),
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

  const _TabLabel({required this.label, required this.selected, required this.onTap});

  @override
  Widget build(BuildContext context) {
    final isDark = Theme.of(context).brightness == Brightness.dark;
    final primary = Theme.of(context).colorScheme.primary;
    final tertiaryText = isDark ? AppColors.darkTextTertiary : AppColors.lightTextTertiary;
    return InkWell(
      onTap: onTap,
      child: Container(
        padding: const EdgeInsets.only(bottom: 10),
        decoration: BoxDecoration(
          border: Border(bottom: BorderSide(color: selected ? primary : Colors.transparent, width: 2)),
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

/// One ticket under its event's heading.
///
/// Deliberately no event name on the card — the heading above already said it, and repeating it on
/// every stub is what made the old flat list read as a wall of identical rows. What is left is
/// exactly what separates this ticket from its siblings: its date block, its sector and type.
class _TicketStubCard extends StatelessWidget {
  final TicketResponse ticket;
  final VoidCallback onTap;

  const _TicketStubCard({required this.ticket, required this.onTap});

  DateTime get _headlineDate => ticket.validDate ?? ticket.validFrom ?? ticket.createdAt;

  static const _months = [
    'JAN', 'FEB', 'MAR', 'APR', 'MAJ', 'JUN',
    'JUL', 'AVG', 'SEP', 'OKT', 'NOV', 'DEC',
  ];

  /// A subscription covers a period, not a day, so it says so instead of showing a single date
  /// that means nothing on its own.
  String? get _periodLabel {
    final from = ticket.validFrom;
    final to = ticket.validTo;
    if (from == null || to == null) return null;
    return 'Važi ${from.day}.${from.month}. – ${to.day}.${to.month}.${to.year}.';
  }

  @override
  Widget build(BuildContext context) {
    final isDark = Theme.of(context).brightness == Brightness.dark;
    final tertiaryText = isDark ? AppColors.darkTextTertiary : AppColors.lightTextTertiary;
    final date = _headlineDate;
    final period = _periodLabel;

    return InkWell(
      borderRadius: BorderRadius.circular(14),
      onTap: onTap,
      child: Card(
        clipBehavior: Clip.antiAlias,
        margin: EdgeInsets.zero,
        child: SizedBox(
          height: 84,
          child: Row(
            children: [
              Container(
                width: 72,
                decoration: const BoxDecoration(
                  gradient: LinearGradient(colors: [AppColors.primary, AppColors.secondary]),
                ),
                child: Column(
                  mainAxisAlignment: MainAxisAlignment.center,
                  children: [
                    Text(_months[date.month - 1], style: const TextStyle(fontSize: 10, color: Colors.white70)),
                    Text(
                      '${date.day}',
                      style: const TextStyle(fontSize: 20, fontWeight: FontWeight.w700, color: Colors.white),
                    ),
                  ],
                ),
              ),
              Expanded(
                child: Padding(
                  padding: const EdgeInsets.symmetric(horizontal: 14),
                  child: Column(
                    crossAxisAlignment: CrossAxisAlignment.start,
                    mainAxisAlignment: MainAxisAlignment.center,
                    mainAxisSize: MainAxisSize.min,
                    children: [
                      Text(
                        ticket.ticketTypeName != null
                            ? '${ticket.sectorName} · ${ticket.ticketTypeName}'
                            : ticket.sectorName,
                        style: const TextStyle(fontSize: 14, fontWeight: FontWeight.w700),
                        maxLines: 1,
                        overflow: TextOverflow.ellipsis,
                      ),
                      const SizedBox(height: 3),
                      Text(
                        period ?? 'Prikaži QR kod',
                        style: TextStyle(fontSize: 12, color: tertiaryText),
                        maxLines: 1,
                        overflow: TextOverflow.ellipsis,
                      ),
                    ],
                  ),
                ),
              ),
              Padding(
                padding: const EdgeInsets.only(right: 12),
                child: Icon(Icons.chevron_right_rounded, size: 18, color: tertiaryText),
              ),
            ],
          ),
        ),
      ),
    );
  }
}
