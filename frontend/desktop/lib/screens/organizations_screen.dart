import 'dart:async';

import 'package:flutter/material.dart';
import 'package:lucide_icons_flutter/lucide_icons.dart';
import '../models/responses/organization_response.dart';
import '../models/search_objects/organization_search_object.dart';
import '../providers/product_organization_ids_provider.dart';
import '../providers/organization_provider.dart';
import '../theme/app_colors.dart';
import 'organization_detail_screen.dart';
import 'widgets/category_multi_select_filter.dart';
import 'widgets/entity_avatar.dart';
import 'widgets/pagination_bar.dart';
import 'widgets/organization_upsert_dialog.dart';
import '../main.dart';

class OrganizationsScreen extends StatefulWidget {
  const OrganizationsScreen({super.key});

  @override
  State<OrganizationsScreen> createState() => _OrganizationsScreenState();
}

class _OrganizationsScreenState extends State<OrganizationsScreen> {
  final _searchController = TextEditingController();
  final OrganizationProvider _provider = OrganizationProvider();
  Timer? _debounce;

  List<OrganizationResponse> _organizations = [];
  bool _isLoading = true;
  List<int> _selectedCategoryIds = [];

  int _currentPage = 0;
  int _totalCount = 0;
  int _pageSize = 10;

  // Bumped at the start of every _loadData() call — a response is only applied if its captured
  // token still matches this field, so a stale (superseded) request can't overwrite the results
  // of a newer one that started after it (e.g. rapid category-filter toggling).
  int _requestToken = 0;

  int get _totalPages => (_totalCount / _pageSize).ceil().clamp(1, 99999);

  @override
  void initState() {
    super.initState();
    _loadData();
  }

  void _onSearchChanged(String _) {
    if (_debounce?.isActive ?? false) _debounce!.cancel();
    _debounce = Timer(const Duration(milliseconds: 300), () {
      setState(() => _currentPage = 0);
      _loadData();
    });
  }

  void _onCategoryFilterChanged(List<int> categoryIds) {
    setState(() {
      _selectedCategoryIds = categoryIds;
      _currentPage = 0;
    });
    _loadData();
  }

  Future<void> _loadData() async {
    final requestToken = ++_requestToken;
    setState(() => _isLoading = true);
    try {
      // Organizations and Products/Categories live in separate microservices/
      // databases — the category filter can't be applied as a server-side
      // join here, so it's resolved client-side in two steps: first ask
      // Catalog which organizations have products in the selected categories,
      // then filter the organizations list by that id list.
      List<String>? organizationIds;
      if (_selectedCategoryIds.isNotEmpty) {
        organizationIds = await ProductOrganizationIdsProvider()
            .getOrganizationIds(categoryIds: _selectedCategoryIds);

        // A newer _loadData() call started while we were awaiting above —
        // its result (or the one after it) is what should win, not this one.
        if (requestToken != _requestToken) return;

        if (organizationIds.isEmpty) {
          // No organization has products in the selected categories. An empty
          // (but non-null) list can't be sent as a real "match nothing"
          // filter here — Dio's list-query encoding produces zero repeated
          // keys for an empty list, so on the wire this would be
          // indistinguishable from omitting OrganizationIds entirely, which
          // the backend (correctly, for the "no filter" case) treats as "no
          // filter" rather than "match nothing" — see
          // OrganizationRepository.SearchAsync's null-or-empty check. Render
          // the empty result directly instead of making a request that
          // would silently come back as "all organizations".
          if (mounted) {
            setState(() {
              _organizations = [];
              _totalCount = 0;
              _isLoading = false;
            });
          }
          return;
        }
      }

      final searchObject = OrganizationSearchObject(
        page: _currentPage,
        pageSize: _pageSize,
        fts: _searchController.text.trim().isEmpty
            ? null
            : _searchController.text.trim(),
        organizationIds: organizationIds,
      );
      final result = await _provider.getAll(
        searchObject: searchObject,
        fromJson: OrganizationResponse.fromJson,
      );

      if (requestToken != _requestToken) return; // superseded by a newer request

      if (mounted) {
        setState(() {
          _organizations = result.items;
          _totalCount = result.totalCount;
          _isLoading = false;
        });
      }
    } catch (e) {
      if (requestToken != _requestToken) return;
      if (mounted) {
        setState(() => _isLoading = false);
        handleApiError(e);
      }
    }
  }

  void _openDialog({OrganizationResponse? organization}) {
    showDialog(
      context: context,
      barrierDismissible: false,
      builder: (_) => OrganizationUpsertDialog(
        organization: organization,
        onSaved: _loadData,
      ),
    );
  }

  Future<void> _deleteOrganization(OrganizationResponse org) async {
    final confirmed = await showDialog<bool>(
      context: context,
      builder: (ctx) {
        final isDark = Theme.of(ctx).brightness == Brightness.dark;
        return AlertDialog(
          backgroundColor: isDark ? AppColors.darkSurface : Colors.white,
          shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(16)),
          title: Text(
            'Obriši organizaciju',
            style: TextStyle(
              fontWeight: FontWeight.w700,
              color: isDark ? AppColors.darkTextPrimary : AppColors.lightTextPrimary,
            ),
          ),
          content: Text(
            'Da li ste sigurni da želite obrisati organizaciju "${org.name}"?',
            style: TextStyle(
              color: isDark ? AppColors.darkTextSecondary : AppColors.lightTextSecondary,
            ),
          ),
          actions: [
            TextButton(
              onPressed: () => Navigator.of(ctx).pop(false),
              child: Text(
                'Odustani',
                style: TextStyle(
                  color: isDark ? AppColors.darkTextTertiary : AppColors.lightTextTertiary,
                ),
              ),
            ),
            ElevatedButton(
              style: ElevatedButton.styleFrom(
                backgroundColor: AppColors.errorDark,
                foregroundColor: Colors.white,
                shape: RoundedRectangleBorder(
                    borderRadius: BorderRadius.circular(10)),
              ),
              onPressed: () => Navigator.of(ctx).pop(true),
              child: const Text('Obriši'),
            ),
          ],
        );
      },
    );

    if (confirmed != true || !mounted) return;

    try {
      await _provider.delete(org.id);
      if (!mounted) return;

      if (_organizations.length == 1 && _currentPage > 0) {
        setState(() => _currentPage--);
      }
      await _loadData();

      if (mounted) {
        final isDark = Theme.of(context).brightness == Brightness.dark;
        final onColor = isDark ? AppColors.darkBackground : Colors.white;
        ScaffoldMessenger.of(context).showSnackBar(
          SnackBar(
            content: Row(
              children: [
                Icon(Icons.check_circle_outline, color: onColor),
                const SizedBox(width: 10),
                Text('Organizacija "${org.name}" je uspješno obrisana',
                    style: TextStyle(color: onColor)),
              ],
            ),
            backgroundColor: isDark ? AppColors.secondary : AppColors.primary,
            behavior: SnackBarBehavior.floating,
            shape:
                RoundedRectangleBorder(borderRadius: BorderRadius.circular(10)),
            margin: const EdgeInsets.all(16),
            duration: const Duration(seconds: 3),
          ),
        );
      }
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
  void dispose() {
    _debounce?.cancel();
    _searchController.dispose();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    final isDark = Theme.of(context).brightness == Brightness.dark;
    final primaryColor = isDark ? AppColors.secondary : AppColors.primary;
    final primaryDarkColor = isDark ? AppColors.primary : AppColors.primaryDark;
    final onPrimaryColor = isDark ? AppColors.darkBackground : Colors.white;
    final textPrimary = isDark ? AppColors.darkTextPrimary : AppColors.lightTextPrimary;
    final textTertiary = isDark ? AppColors.darkTextTertiary : AppColors.lightTextTertiary;
    final placeholderColor = isDark ? AppColors.darkTextTertiary : AppColors.lightTextDisabled;

    // The whole page is one SingleChildScrollView (not header-fixed +
    // internally-scrolling grid) — scrolling moves the title/search/count out
    // of view along with everything else, so the pagination bar is always
    // reachable by scrolling the same way as the rest of the content, not
    // hidden behind a separately-scrolled grid region.
    return SingleChildScrollView(
      padding: const EdgeInsets.all(24),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          // ── Header ─────────────────────────────────────────────────
          Row(
            mainAxisAlignment: MainAxisAlignment.spaceBetween,
            children: [
              Expanded(
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    Text(
                      'Organizacije',
                      style: TextStyle(
                        fontSize: 30,
                        fontWeight: FontWeight.w700,
                        color: textPrimary,
                        height: 1.2,
                      ),
                    ),
                    const SizedBox(height: 8),
                    Text(
                      'Upravljajte svim organizacijama na platformi',
                      style: TextStyle(fontSize: 16, color: textTertiary),
                    ),
                  ],
                ),
              ),
              const SizedBox(width: 16),
              Container(
                decoration: BoxDecoration(
                  gradient: LinearGradient(
                    colors: [primaryColor, primaryDarkColor],
                  ),
                  borderRadius: BorderRadius.circular(12),
                  boxShadow: [
                    BoxShadow(
                      color: primaryColor.withValues(alpha: 0.3),
                      blurRadius: 10,
                      offset: const Offset(0, 4),
                    ),
                  ],
                ),
                child: Material(
                  color: Colors.transparent,
                  child: InkWell(
                    borderRadius: BorderRadius.circular(12),
                    onTap: () => _openDialog(),
                    child: Padding(
                      padding: const EdgeInsets.symmetric(
                          horizontal: 16, vertical: 10),
                      child: Row(
                        children: [
                          Icon(LucideIcons.plus, color: onPrimaryColor, size: 20),
                          const SizedBox(width: 8),
                          Text(
                            'Dodaj Organizaciju',
                            style: TextStyle(
                              color: onPrimaryColor,
                              fontWeight: FontWeight.w600,
                            ),
                          ),
                        ],
                      ),
                    ),
                  ),
                ),
              ),
            ],
          ),

          const SizedBox(height: 24),

          // ── Search + category filter ─────────────────────────────────
          Row(
            children: [
              Expanded(
                child: Container(
                  height: 44,
                  decoration: BoxDecoration(
                    color: isDark ? AppColors.darkSurface : Colors.white,
                    border: Border.all(
                        color: isDark ? AppColors.darkBorderInput : AppColors.lightBorderInput),
                    borderRadius: BorderRadius.circular(12),
                  ),
                  child: TextField(
                    controller: _searchController,
                    onChanged: _onSearchChanged,
                    decoration: InputDecoration(
                      hintText: 'Pretražite organizacije...',
                      hintStyle: TextStyle(color: textTertiary),
                      prefixIcon: Icon(LucideIcons.search,
                          color: placeholderColor, size: 18),
                      border: InputBorder.none,
                      contentPadding: const EdgeInsets.symmetric(vertical: 12),
                    ),
                    style: TextStyle(color: textPrimary),
                  ),
                ),
              ),
              const SizedBox(width: 12),
              CategoryMultiSelectFilter(
                selectedCategoryIds: _selectedCategoryIds,
                onChanged: _onCategoryFilterChanged,
              ),
            ],
          ),

          const SizedBox(height: 12),

          // ── Count ─────────────────────────────────────────────────
          if (!_isLoading)
            Padding(
              padding: const EdgeInsets.only(bottom: 8),
              child: Text(
                '$_totalCount organizacija',
                style: TextStyle(fontSize: 13, color: textTertiary),
              ),
            ),

          // ── Grid ── not wrapped in Expanded (the page is one scroll view,
          // not header-fixed + internally-scrolling grid) — loading/empty
          // states get an explicit height since there's no ambient Expanded
          // to size them anymore.
          if (_isLoading)
            SizedBox(
              height: 300,
              child: Center(child: CircularProgressIndicator(color: primaryColor)),
            )
          else if (_organizations.isEmpty)
            SizedBox(
              height: 300,
              child: Center(
                child: Column(
                  mainAxisSize: MainAxisSize.min,
                  children: [
                    Icon(LucideIcons.building2,
                        size: 48,
                        color: isDark ? AppColors.darkBorderInput : AppColors.lightBorderInput),
                    const SizedBox(height: 12),
                    Text(
                      'Nema organizacija',
                      style: TextStyle(color: textTertiary, fontSize: 16),
                    ),
                  ],
                ),
              ),
            )
          else
            GridView.builder(
              // MaxCrossAxisExtent (not a breakpoint-driven fixed count) caps how
              // wide any single card can get — a fixed crossAxisCount let a card
              // grow arbitrarily large (e.g. ~380px wide/~530px tall on a >2000px
              // monitor with few results), which looked broken even though nothing
              // overflowed. This way more columns simply appear as the window
              // widens instead of existing cards stretching. mainAxisExtent (a
              // fixed pixel height, not an aspect ratio) is generous enough for the
              // card's tallest realistic content, so it can never overflow either.
              shrinkWrap: true,
              physics: const NeverScrollableScrollPhysics(),
              gridDelegate: const SliverGridDelegateWithMaxCrossAxisExtent(
                maxCrossAxisExtent: 300,
                mainAxisExtent: 380,
                crossAxisSpacing: 14,
                mainAxisSpacing: 14,
              ),
              itemCount: _organizations.length,
              itemBuilder: (context, index) {
                final org = _organizations[index];
                return _OrganizationCard(
                  organization: org,
                  onView: () async {
                    // The detail screen can edit its local organization (e.g. add
                    // logo, rename) — await the route so this list reloads and
                    // picks up any change instead of showing a stale card.
                    await Navigator.push(
                      context,
                      MaterialPageRoute(
                        builder: (_) => OrganizationDetailScreen(organization: org),
                      ),
                    );
                    if (mounted) _loadData();
                  },
                  onEdit: () => _openDialog(organization: org),
                  onDelete: () => _deleteOrganization(org),
                );
              },
            ),

          // ── Pagination ───────────────────────────────────────────
          Padding(
            padding: const EdgeInsets.only(top: 16),
            child: PaginationBar(
              currentPage: _currentPage,
              totalPages: _totalPages,
              onPageChanged: _goToPage,
              pageSize: _pageSize,
              onPageSizeChanged: _onPageSizeChanged,
            ),
          ),
        ],
      ),
    );
  }
}

// ─────────────────────────────────────────────────────────────
//  Organization Card
// ─────────────────────────────────────────────────────────────
class _OrganizationCard extends StatefulWidget {
  final OrganizationResponse organization;
  final VoidCallback onView;
  final VoidCallback onEdit;
  final VoidCallback onDelete;

  const _OrganizationCard({
    required this.organization,
    required this.onView,
    required this.onEdit,
    required this.onDelete,
  });

  @override
  State<_OrganizationCard> createState() => _OrganizationCardState();
}

class _OrganizationCardState extends State<_OrganizationCard> {
  bool _isHovering = false;

  @override
  Widget build(BuildContext context) {
    final org = widget.organization;
    final isDark = Theme.of(context).brightness == Brightness.dark;
    final primaryColor = isDark ? AppColors.secondary : AppColors.primary;
    final textPrimary = isDark ? AppColors.darkTextPrimary : AppColors.lightTextPrimary;

    return MouseRegion(
      onEnter: (_) => setState(() => _isHovering = true),
      onExit: (_) => setState(() => _isHovering = false),
      cursor: SystemMouseCursors.click,
      child: AnimatedContainer(
        duration: const Duration(milliseconds: 200),
        decoration: BoxDecoration(
          color: isDark ? AppColors.darkSurface : Colors.white,
          borderRadius: BorderRadius.circular(16),
          border: Border.all(
            color: _isHovering
                ? primaryColor
                : (isDark ? AppColors.darkBorder : AppColors.lightBorder),
          ),
          boxShadow: _isHovering && !isDark
              ? [
                  BoxShadow(
                    color: Colors.black.withValues(alpha: 0.08),
                    blurRadius: 12,
                    offset: const Offset(0, 4),
                  ),
                ]
              : null,
        ),
        child: Padding(
          padding: const EdgeInsets.all(16),
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              // ── Logo ── 72 (not the previous 100) to match the card's new
              // fixed, more compact height (see the grid's mainAxisExtent).
              Center(
                child: EntityAvatar(name: org.name, logoUrl: org.logoUrl, size: 72),
              ),

              const SizedBox(height: 16),

              // ── Name ──
              Text(
                org.name,
                style: TextStyle(
                  fontSize: 16,
                  fontWeight: FontWeight.w700,
                  color: textPrimary,
                ),
                maxLines: 1,
                overflow: TextOverflow.ellipsis,
                textAlign: TextAlign.center,
              ),

              const SizedBox(height: 12),

              // ── Contact info ── plain (not Expanded/Flexible) — the card now has
              // a fixed pixel height (see the grid's mainAxisExtent), not an
              // elastic aspect-ratio height, so there's no "remaining space" that
              // needs to be measured and clamped at build time. Wrapping this in
              // Expanded previously forced it into whatever leftover height the
              // Column's flex algorithm computed, which could be (and was)
              // smaller than the 3 rows' actual natural height, overflowing
              // regardless of the inner Flexible. Laying it out top-down at
              // natural size and giving the card generous fixed height headroom
              // instead is what actually guarantees no overflow.
              _ContactRow(emoji: '📧', text: org.email),
              const SizedBox(height: 6),
              _ContactRow(emoji: '📞', text: org.phoneNumber),
              const SizedBox(height: 6),
              _ContactRow(emoji: '📍', text: org.address),

              const SizedBox(height: 12),

              // ── User count chip ──
              Container(
                padding: const EdgeInsets.symmetric(horizontal: 10, vertical: 5),
                decoration: BoxDecoration(
                  color: primaryColor.withValues(alpha: isDark ? 0.16 : 0.08),
                  borderRadius: BorderRadius.circular(8),
                ),
                child: Row(
                  mainAxisSize: MainAxisSize.min,
                  children: [
                    Icon(LucideIcons.users, size: 14, color: primaryColor),
                    const SizedBox(width: 6),
                    Text(
                      '${org.userCount} korisnika',
                      style: TextStyle(
                        fontSize: 13,
                        fontWeight: FontWeight.w600,
                        color: primaryColor,
                      ),
                    ),
                  ],
                ),
              ),

              // Spacer (not Expanded around fixed content) — safe by
              // construction, its minimum size is 0, so it can never be the
              // cause of an overflow; it only pushes the buttons down when
              // there's genuine leftover room.
              const Spacer(),

              const SizedBox(height: 14),

              // ── Action buttons ──
              Row(
                children: [
                  Expanded(
                    child: _ActionButton(
                      icon: LucideIcons.eye,
                      label: 'Pregled',
                      isPrimary: true,
                      onTap: widget.onView,
                    ),
                  ),
                  const SizedBox(width: 8),
                  _IconOnlyButton(
                    icon: LucideIcons.pencil,
                    tooltip: 'Uredi organizaciju',
                    onTap: widget.onEdit,
                  ),
                  const SizedBox(width: 8),
                  Expanded(
                    child: _ActionButton(
                      icon: LucideIcons.trash2,
                      label: 'Obriši',
                      isPrimary: false,
                      isDestructive: true,
                      onTap: widget.onDelete,
                    ),
                  ),
                ],
              ),
            ],
          ),
        ),
      ),
    );
  }
}

// ─── Contact row ──────────────────────────────────────────────────────────────

class _ContactRow extends StatelessWidget {
  final String emoji;
  final String text;

  const _ContactRow({required this.emoji, required this.text});

  @override
  Widget build(BuildContext context) {
    final isDark = Theme.of(context).brightness == Brightness.dark;
    final textTertiary = isDark ? AppColors.darkTextTertiary : AppColors.lightTextTertiary;
    return Row(
      children: [
        Text(emoji, style: const TextStyle(fontSize: 13)),
        const SizedBox(width: 8),
        Expanded(
          child: Text(
            text,
            style: TextStyle(fontSize: 13, color: textTertiary),
            maxLines: 1,
            overflow: TextOverflow.ellipsis,
          ),
        ),
      ],
    );
  }
}

// ─── Action button ────────────────────────────────────────────────────────────

class _ActionButton extends StatelessWidget {
  final IconData icon;
  final String label;
  final bool isPrimary;
  final bool isDestructive;
  final VoidCallback onTap;

  const _ActionButton({
    required this.icon,
    required this.label,
    required this.isPrimary,
    required this.onTap,
    this.isDestructive = false,
  });

  @override
  Widget build(BuildContext context) {
    final isDark = Theme.of(context).brightness == Brightness.dark;
    final primaryColor = isDark ? AppColors.secondary : AppColors.primary;

    final Color fg;
    final Color bg;
    if (isDestructive) {
      fg = AppColors.error;
      bg = AppColors.error.withValues(alpha: isDark ? 0.16 : 0.08);
    } else if (isPrimary) {
      fg = primaryColor;
      bg = primaryColor.withValues(alpha: isDark ? 0.18 : 0.1);
    } else {
      fg = isDark ? AppColors.darkTextSecondary : AppColors.lightTextSecondary;
      bg = isDark ? AppColors.darkSurfaceMuted : AppColors.lightSurfaceMuted;
    }

    return Material(
      color: bg,
      borderRadius: BorderRadius.circular(12),
      child: InkWell(
        borderRadius: BorderRadius.circular(12),
        onTap: onTap,
        child: Container(
          padding: const EdgeInsets.symmetric(vertical: 9, horizontal: 10),
          decoration: (isPrimary || isDestructive)
              ? BoxDecoration(
                  borderRadius: BorderRadius.circular(12),
                  border: Border.all(color: fg.withValues(alpha: 0.2)),
                )
              : null,
          child: Row(
            mainAxisAlignment: MainAxisAlignment.center,
            children: [
              Icon(icon, size: 16, color: fg),
              const SizedBox(width: 6),
              // Flexible+ellipsis (not a bare Text) — this button sits inside an
              // Expanded whose actual width depends on the grid's computed
              // column count, which can be narrower than the card's nominal
              // maxCrossAxisExtent (SliverGridDelegateWithMaxCrossAxisExtent may
              // pick more, narrower columns to fill the row). Without this, the
              // label previously caused a RenderFlex right-overflow at some
              // window widths instead of just truncating.
              Flexible(
                child: Text(
                  label,
                  style: TextStyle(
                    fontSize: 13,
                    fontWeight: FontWeight.w600,
                    color: fg,
                  ),
                  maxLines: 1,
                  overflow: TextOverflow.ellipsis,
                ),
              ),
            ],
          ),
        ),
      ),
    );
  }
}

// ─── Icon-only button (edit, next to the primary "Pregled" action) ────────────

class _IconOnlyButton extends StatelessWidget {
  final IconData icon;
  final VoidCallback onTap;
  final String tooltip;

  const _IconOnlyButton({required this.icon, required this.onTap, required this.tooltip});

  @override
  Widget build(BuildContext context) {
    final isDark = Theme.of(context).brightness == Brightness.dark;
    return Tooltip(
      message: tooltip,
      child: Material(
        color: isDark ? AppColors.darkSurfaceMuted : AppColors.lightSurfaceMuted,
        borderRadius: BorderRadius.circular(12),
        child: InkWell(
          borderRadius: BorderRadius.circular(12),
          onTap: onTap,
          child: Container(
            width: 40,
            height: 38,
            alignment: Alignment.center,
            decoration: BoxDecoration(
              borderRadius: BorderRadius.circular(12),
              border: Border.all(color: isDark ? AppColors.darkBorder : AppColors.lightBorder),
            ),
            child: Icon(icon,
                size: 16,
                color: isDark ? AppColors.darkTextSecondary : AppColors.lightTextSecondary),
          ),
        ),
      ),
    );
  }
}
