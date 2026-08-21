import 'dart:async';

import 'package:flutter/material.dart';
import 'package:lucide_icons_flutter/lucide_icons.dart';

import '../models/responses/admin_user_response.dart';
import '../models/responses/product_response.dart';
import '../models/responses/organization_response.dart';
import '../models/search_objects/base_search_object.dart';
import '../models/search_objects/organization_user_search_object.dart';
import '../providers/organization_products_provider.dart';
import '../providers/organization_provider.dart';
import '../providers/product_provider.dart';
import '../providers/user_provider.dart';
import '../theme/app_colors.dart';
import '../widgets/confirm_dialog.dart';
import 'widgets/entity_avatar.dart';
import 'widgets/organization_upsert_dialog.dart';
import 'widgets/pagination_bar.dart';
import 'widgets/paginated_screen_body.dart';
import 'widgets/product_upsert_dialog.dart';
import 'widgets/stat_card.dart';
import 'widgets/user_grid_card.dart';
import '../main.dart';

/// Per-tab paginated/searchable list state — instantiated once per tab
/// (Products / Users) so each tab paginates and searches fully independently,
/// mirroring the single-list state shape used by UsersScreen/
/// OrganizationsScreen but generic over 2 simultaneous lists. pageSize is
/// per-tab (not a single screen-wide constant) so each tab's card grid can
/// have its own "cards per page" independently.
class _TabState<T> {
  List<T> items = [];
  int totalCount = 0;
  int currentPage = 0;
  int pageSize = 10;
  bool isLoading = true;
  final searchController = TextEditingController();
  Timer? debounce;

  int get totalPages => (totalCount / pageSize).ceil().clamp(1, 99999);

  void dispose() {
    debounce?.cancel();
    searchController.dispose();
  }
}

/// Superadmin-facing organization detail screen: header + product/user counts
/// + independently paginated/searchable Products and Users tabs (Users spans
/// both OrganizationSuperAdmin and OrganizationAdmin accounts — there's no
/// role split here, unlike the desktop app's own self-service Users screen).
/// The Products tab is full CRUD (not read-only) — PlatformStaff has
/// override on every organization's products/sectors, see [[01-domain]].
/// Reached only via the org card's "Pregled" action on OrganizationsScreen —
/// there's no named-route table anywhere in this app, so this is pushed via
/// `Navigator.push(MaterialPageRoute(...))`, not a sidebar destination.
class OrganizationDetailScreen extends StatefulWidget {
  final OrganizationResponse organization;

  const OrganizationDetailScreen({super.key, required this.organization});

  @override
  State<OrganizationDetailScreen> createState() => _OrganizationDetailScreenState();
}

class _OrganizationDetailScreenState extends State<OrganizationDetailScreen>
    with SingleTickerProviderStateMixin {
  late TabController _tabController;
  late OrganizationResponse _organization;

  final _productsState = _TabState<ProductResponse>();
  final _usersState = _TabState<AdminUserResponse>();

  bool get _isDark => Theme.of(context).brightness == Brightness.dark;

  @override
  void initState() {
    super.initState();
    _organization = widget.organization;
    _tabController = TabController(length: 2, vsync: this);
    Future.wait([_loadProducts(), _loadUsers()]);
  }

  @override
  void dispose() {
    _tabController.dispose();
    _productsState.dispose();
    _usersState.dispose();
    super.dispose();
  }

  // ── Data loading ─────────────────────────────────────────────────────────

  Future<void> _loadProducts() async {
    setState(() => _productsState.isLoading = true);
    try {
      final searchObject = BaseSearchObject(
        page: _productsState.currentPage,
        pageSize: _productsState.pageSize,
        fts: _productsState.searchController.text.trim().isEmpty
            ? null
            : _productsState.searchController.text.trim(),
      );
      final result = await OrganizationProductsProvider().getAll(
        organizationId: _organization.id,
        searchObject: searchObject,
        fromJson: ProductResponse.fromJson,
      );
      if (mounted) {
        setState(() {
          _productsState.items = result.items;
          _productsState.totalCount = result.totalCount;
          _productsState.isLoading = false;
        });
      }
    } catch (e) {
      if (mounted) {
        setState(() => _productsState.isLoading = false);
        handleApiError(e);
      }
    }
  }

  Future<void> _editProduct(ProductResponse product) async {
    await showDialog(
      context: context,
      barrierDismissible: false,
      builder: (_) => ProductUpsertDialog(product: product, onSaved: (_) => _loadProducts()),
    );
  }

  Future<void> _deleteProduct(ProductResponse product) async {
    final confirmed = await ConfirmDialog.show(
      context,
      title: 'Obriši proizvod',
      message: 'Da li ste sigurni da želite obrisati proizvod "${product.name}"?',
      confirmLabel: 'Obriši',
    );

    if (confirmed != true || !mounted) return;

    try {
      await ProductProvider().delete(product.id);
      if (mounted) await _loadProducts();
    } catch (e) {
      if (mounted) handleApiError(e);
    }
  }

  /// All of the organization's users (both OrganizationSuperAdmin and
  /// OrganizationAdmin) — no role filter, unlike the self-service Users
  /// screen's org view (which is deliberately scoped to OrganizationAdmin
  /// only). This is the platform-staff read view of "who's in this org".
  Future<void> _loadUsers() async {
    setState(() => _usersState.isLoading = true);
    try {
      final searchObject = OrganizationUserSearchObject(
        page: _usersState.currentPage,
        pageSize: _usersState.pageSize,
        fts: _usersState.searchController.text.trim().isEmpty
            ? null
            : _usersState.searchController.text.trim(),
      );
      final result = await OrganizationUsersProvider().getAll(
        organizationId: _organization.id,
        searchObject: searchObject,
        fromJson: AdminUserResponse.fromJson,
      );
      if (mounted) {
        setState(() {
          _usersState.items = result.items;
          _usersState.totalCount = result.totalCount;
          _usersState.isLoading = false;
        });
      }
    } catch (e) {
      if (mounted) {
        setState(() => _usersState.isLoading = false);
        handleApiError(e);
      }
    }
  }

  void _onSearchChanged(_TabState<dynamic> state, Future<void> Function() reload) {
    if (state.debounce?.isActive ?? false) state.debounce!.cancel();
    state.debounce = Timer(const Duration(milliseconds: 300), () {
      setState(() => state.currentPage = 0);
      reload();
    });
  }

  void _goToPage(_TabState<dynamic> state, int page, Future<void> Function() reload) {
    if (page < 0 || page >= state.totalPages) return;
    setState(() => state.currentPage = page);
    reload();
  }

  void _onPageSizeChanged(_TabState<dynamic> state, int size, Future<void> Function() reload) {
    setState(() {
      state.pageSize = size;
      state.currentPage = 0;
    });
    reload();
  }

  Future<void> _editOrganization() async {
    await showDialog(
      context: context,
      barrierDismissible: false,
      builder: (_) => OrganizationUpsertDialog(
        organization: _organization,
        onSaved: () async {
          final refreshed = await OrganizationProvider().getOrganization(_organization.id);
          if (mounted) setState(() => _organization = refreshed);
        },
      ),
    );
  }

  // ── Build ─────────────────────────────────────────────────────────────────

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
        title: Text(_organization.name, style: const TextStyle(fontWeight: FontWeight.w700)),
        actions: [
          TextButton.icon(
            onPressed: _editOrganization,
            icon: Icon(LucideIcons.pencil, size: 16, color: primary),
            label: Text('Uredi', style: TextStyle(color: primary, fontWeight: FontWeight.w600)),
          ),
          const SizedBox(width: 12),
        ],
      ),
      // TabBarView (a PageView under the hood, which needs a bounded height) is
      // gone — TabController + TabBar stay only as the visual tab-selector strip,
      // driving which single tab's content is rendered inline below it, no swipe
      // gesture needed on a desktop app. The pagination bar for whichever tab is
      // active is pinned to the bottom of the viewport (see PaginatedScreenBody) —
      // only the org header/stat cards/tab selector/grid above it scroll.
      body: PaginatedScreenBody(
        pagination: _tabController.index == 0
            ? PaginationBar(
                currentPage: _productsState.currentPage,
                totalPages: _productsState.totalPages,
                onPageChanged: (page) => _goToPage(_productsState, page, _loadProducts),
                pageSize: _productsState.pageSize,
                onPageSizeChanged: (size) => _onPageSizeChanged(_productsState, size, _loadProducts),
              )
            : PaginationBar(
                currentPage: _usersState.currentPage,
                totalPages: _usersState.totalPages,
                onPageChanged: (page) => _goToPage(_usersState, page, _loadUsers),
                pageSize: _usersState.pageSize,
                onPageSizeChanged: (size) => _onPageSizeChanged(_usersState, size, _loadUsers),
              ),
        content: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            _buildHeader(isDark, textPrimary, textTertiary),
            const SizedBox(height: 24),
            Row(
              children: [
                StatCard(
                  label: 'Broj Proizvoda',
                  value: '${_productsState.totalCount}',
                  valueColor: textPrimary,
                ),
                const SizedBox(width: 16),
                StatCard(
                  label: 'Broj Korisnika',
                  value: '${_usersState.totalCount}',
                  valueColor: primary,
                ),
              ],
            ),
            const SizedBox(height: 24),
            TabBar(
              controller: _tabController,
              isScrollable: true,
              labelColor: primary,
              unselectedLabelColor: textTertiary,
              indicatorColor: primary,
              onTap: (_) => setState(() {}),
              tabs: const [
                Tab(text: 'Proizvodi'),
                Tab(text: 'Korisnici'),
              ],
            ),
            const SizedBox(height: 16),
            _tabController.index == 0
                ? _buildProductsTab(isDark, textPrimary, textTertiary)
                : _buildUsersTab(isDark, textPrimary, textTertiary),
          ],
        ),
      ),
    );
  }

  Widget _buildHeader(bool isDark, Color textPrimary, Color textTertiary) {
    return Container(
      padding: const EdgeInsets.all(20),
      decoration: BoxDecoration(
        color: isDark ? AppColors.darkSurface : Colors.white,
        border: Border.all(color: isDark ? AppColors.darkBorder : AppColors.lightBorder),
        borderRadius: BorderRadius.circular(16),
      ),
      child: Row(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          EntityAvatar(name: _organization.name, logoUrl: _organization.logoUrl, size: 72),
          const SizedBox(width: 20),
          Expanded(
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Row(
                  children: [
                    Flexible(
                      child: Text(
                        _organization.name,
                        style: TextStyle(fontSize: 20, fontWeight: FontWeight.w700, color: textPrimary),
                        maxLines: 1,
                        overflow: TextOverflow.ellipsis,
                      ),
                    ),
                    const SizedBox(width: 10),
                    Container(
                      padding: const EdgeInsets.symmetric(horizontal: 10, vertical: 4),
                      decoration: BoxDecoration(
                        color: (_organization.isActive ? AppColors.success : AppColors.error)
                            .withValues(alpha: isDark ? 0.18 : 0.1),
                        borderRadius: BorderRadius.circular(20),
                      ),
                      child: Text(
                        _organization.isActive ? 'Aktivna' : 'Neaktivna',
                        style: TextStyle(
                          fontSize: 12,
                          fontWeight: FontWeight.w600,
                          color: _organization.isActive ? AppColors.successDark : AppColors.errorDark,
                        ),
                      ),
                    ),
                  ],
                ),
                const SizedBox(height: 12),
                Wrap(
                  spacing: 20,
                  runSpacing: 6,
                  children: [
                    _InfoRow(icon: LucideIcons.mail, text: _organization.email, color: textTertiary),
                    _InfoRow(icon: LucideIcons.phone, text: _organization.phoneNumber, color: textTertiary),
                    _InfoRow(icon: LucideIcons.mapPin, text: _organization.address, color: textTertiary),
                  ],
                ),
              ],
            ),
          ),
        ],
      ),
    );
  }

  // Not wrapped in its own SingleChildScrollView/Expanded — this tab's content
  // is rendered directly inline in the outer page's single SingleChildScrollView
  // (see build()), so it just needs to be a plain, naturally-sized Column.
  Widget _buildProductsTab(bool isDark, Color textPrimary, Color textTertiary) {
    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        _searchField(_productsState, _loadProducts, 'Pretražite proizvode...', isDark, textPrimary, textTertiary),
        const SizedBox(height: 16),
        if (_productsState.isLoading)
          SizedBox(
            height: 300,
            child: Center(child: CircularProgressIndicator(color: isDark ? AppColors.secondary : AppColors.primary)),
          )
        else if (_productsState.items.isEmpty)
          SizedBox(height: 300, child: _emptyState(LucideIcons.packageX, 'Nema proizvoda', isDark, textTertiary))
        else
          _cardGrid(
            itemCount: _productsState.items.length,
            maxCrossAxisExtent: 260,
            mainAxisExtent: 250,
            itemBuilder: (context, index) => _ProductGridCard(
              product: _productsState.items[index],
              isDark: isDark,
              onEdit: () => _editProduct(_productsState.items[index]),
              onDelete: () => _deleteProduct(_productsState.items[index]),
            ),
          ),
      ],
    );
  }

  // Same shape as _buildProductsTab — see its comment.
  Widget _buildUsersTab(bool isDark, Color textPrimary, Color textTertiary) {
    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        _searchField(_usersState, _loadUsers, 'Pretražite po imenu ili emailu...', isDark, textPrimary, textTertiary),
        const SizedBox(height: 16),
        if (_usersState.isLoading)
          SizedBox(
            height: 300,
            child: Center(child: CircularProgressIndicator(color: isDark ? AppColors.secondary : AppColors.primary)),
          )
        else if (_usersState.items.isEmpty)
          SizedBox(height: 300, child: _emptyState(LucideIcons.userX, 'Nema korisnika', isDark, textTertiary))
        else
          _cardGrid(
            itemCount: _usersState.items.length,
            maxCrossAxisExtent: 260,
            mainAxisExtent: 280,
            // Read-only here — this is the platform-staff view of "who's in
            // this org"; editing/deleting org users lives on the desktop app's
            // own self-service Users screen (OrganizationSuperAdmin) and the
            // SuperAdmin platform Users screen, not this detail view.
            itemBuilder: (context, index) => UserGridCard(user: _usersState.items[index]),
          ),
      ],
    );
  }

  // MaxCrossAxisExtent (not a breakpoint-driven fixed count) caps how wide/tall
  // any single card can get — a fixed crossAxisCount let a card grow
  // arbitrarily large on a wide monitor with few results, which looked broken
  // even though nothing overflowed. This way more columns appear as the
  // window widens instead of existing cards stretching. mainAxisExtent (a
  // fixed pixel height per caller, not a shared aspect ratio) lets the Events
  // and Users tabs each size their own card correctly. shrinkWrap +
  // NeverScrollableScrollPhysics because the grid now lives inside the tab's
  // own SingleChildScrollView, not a bounded Expanded — the outer scroll view
  // owns scrolling, the grid just lays out at its natural (full) height.
  Widget _cardGrid({
    required int itemCount,
    required double maxCrossAxisExtent,
    required double mainAxisExtent,
    required Widget Function(BuildContext, int) itemBuilder,
  }) {
    return GridView.builder(
      shrinkWrap: true,
      physics: const NeverScrollableScrollPhysics(),
      gridDelegate: SliverGridDelegateWithMaxCrossAxisExtent(
        maxCrossAxisExtent: maxCrossAxisExtent,
        mainAxisExtent: mainAxisExtent,
        crossAxisSpacing: 14,
        mainAxisSpacing: 14,
      ),
      itemCount: itemCount,
      itemBuilder: itemBuilder,
    );
  }

  Widget _searchField(_TabState<dynamic> state, Future<void> Function() reload, String hint, bool isDark,
      Color textPrimary, Color textTertiary) {
    return Container(
      height: 44,
      decoration: BoxDecoration(
        color: isDark ? AppColors.darkSurface : Colors.white,
        border: Border.all(color: isDark ? AppColors.darkBorderInput : AppColors.lightBorderInput),
        borderRadius: BorderRadius.circular(12),
      ),
      child: TextField(
        controller: state.searchController,
        onChanged: (_) => _onSearchChanged(state, reload),
        decoration: InputDecoration(
          hintText: hint,
          hintStyle: TextStyle(color: textTertiary),
          prefixIcon: Icon(LucideIcons.search,
              color: isDark ? AppColors.darkTextDisabled : AppColors.lightTextDisabled, size: 18),
          border: InputBorder.none,
          contentPadding: const EdgeInsets.symmetric(vertical: 12),
        ),
        style: TextStyle(color: textPrimary),
      ),
    );
  }

  Widget _emptyState(IconData icon, String label, bool isDark, Color textTertiary) {
    return Center(
      child: Column(
        mainAxisSize: MainAxisSize.min,
        children: [
          Icon(icon, size: 48, color: isDark ? AppColors.darkBorderInput : AppColors.lightBorderInput),
          const SizedBox(height: 12),
          Text(label, style: TextStyle(color: textTertiary, fontSize: 16)),
        ],
      ),
    );
  }
}

// ─── Header info row ────────────────────────────────────────────────────────

class _InfoRow extends StatelessWidget {
  final IconData icon;
  final String text;
  final Color color;

  const _InfoRow({required this.icon, required this.text, required this.color});

  @override
  Widget build(BuildContext context) {
    return Row(
      mainAxisSize: MainAxisSize.min,
      children: [
        Icon(icon, size: 14, color: color),
        const SizedBox(width: 6),
        Text(text, style: TextStyle(fontSize: 13, color: color)),
      ],
    );
  }
}

// ─── Product grid card ──────────────────────────────────────────────────────

/// Unlike the read-only card this replaced, this one is hoverable with
/// edit/delete actions — PlatformStaff has full override over every
/// organization's products (see [[01-domain]]), and this screen is only
/// ever reached by PlatformStaff (via OrganizationsScreen).
class _ProductGridCard extends StatefulWidget {
  final ProductResponse product;
  final bool isDark;
  final VoidCallback onEdit;
  final VoidCallback onDelete;

  const _ProductGridCard({
    required this.product,
    required this.isDark,
    required this.onEdit,
    required this.onDelete,
  });

  @override
  State<_ProductGridCard> createState() => _ProductGridCardState();
}

class _ProductGridCardState extends State<_ProductGridCard> {
  bool _isHovering = false;

  String _formatDate(DateTime date) {
    final d = date.toLocal();
    const months = [
      '', 'jan', 'feb', 'mar', 'apr', 'maj', 'jun',
      'jul', 'aug', 'sep', 'okt', 'nov', 'dec',
    ];
    return '${d.day}. ${months[d.month]} ${d.year}.';
  }

  @override
  Widget build(BuildContext context) {
    final isDark = widget.isDark;
    final product = widget.product;
    final textPrimary = isDark ? AppColors.darkTextPrimary : AppColors.lightTextPrimary;
    final textTertiary = isDark ? AppColors.darkTextTertiary : AppColors.lightTextTertiary;
    final statusColor = product.isPublished ? AppColors.success : AppColors.warning;
    final statusColorDark = product.isPublished ? AppColors.successDark : AppColors.warningDark;
    final primary = isDark ? AppColors.secondary : AppColors.primary;

    return MouseRegion(
      onEnter: (_) => setState(() => _isHovering = true),
      onExit: (_) => setState(() => _isHovering = false),
      child: Stack(
        children: [
          Positioned.fill(
            child: Container(
              padding: const EdgeInsets.all(16),
              decoration: BoxDecoration(
                color: isDark ? AppColors.darkSurface : Colors.white,
                borderRadius: BorderRadius.circular(16),
                border: Border.all(
                    color: _isHovering ? primary : (isDark ? AppColors.darkBorder : AppColors.lightBorder)),
              ),
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  Container(
                    width: 44,
                    height: 44,
                    decoration: BoxDecoration(
                      color: primary.withValues(alpha: isDark ? 0.18 : 0.1),
                      borderRadius: BorderRadius.circular(12),
                    ),
                    child: Icon(LucideIcons.package, color: primary, size: 22),
                  ),
                  const SizedBox(height: 12),
                  Text(
                    product.name,
                    style: TextStyle(fontSize: 15, fontWeight: FontWeight.w700, color: textPrimary),
                    maxLines: 2,
                    overflow: TextOverflow.ellipsis,
                  ),
                  const SizedBox(height: 4),
                  Text(
                    product.date != null ? _formatDate(product.date!) : product.ticketingMode.label,
                    style: TextStyle(fontSize: 12, color: textTertiary),
                    maxLines: 1,
                    overflow: TextOverflow.ellipsis,
                  ),
                  const Spacer(),
                  Wrap(
                    spacing: 6,
                    runSpacing: 6,
                    children: [
                      Container(
                        padding: const EdgeInsets.symmetric(horizontal: 10, vertical: 4),
                        decoration: BoxDecoration(
                          color: primary.withValues(alpha: isDark ? 0.18 : 0.1),
                          borderRadius: BorderRadius.circular(20),
                        ),
                        child: Text(
                          product.categoryName,
                          style: TextStyle(fontSize: 11, fontWeight: FontWeight.w600, color: primary),
                          maxLines: 1,
                          overflow: TextOverflow.ellipsis,
                        ),
                      ),
                      Container(
                        padding: const EdgeInsets.symmetric(horizontal: 10, vertical: 4),
                        decoration: BoxDecoration(
                          color: statusColor.withValues(alpha: isDark ? 0.18 : 0.1),
                          borderRadius: BorderRadius.circular(20),
                        ),
                        child: Text(
                          product.isPublished ? 'Objavljen' : 'Nacrt',
                          style: TextStyle(fontSize: 11, fontWeight: FontWeight.w600, color: statusColorDark),
                        ),
                      ),
                    ],
                  ),
                ],
              ),
            ),
          ),
          Positioned(
            top: 8,
            right: 8,
            child: AnimatedOpacity(
              opacity: _isHovering ? 1.0 : 0.0,
              duration: const Duration(milliseconds: 200),
              child: IgnorePointer(
                ignoring: !_isHovering,
                child: Row(
                  mainAxisSize: MainAxisSize.min,
                  children: [
                    _CardActionBtn(icon: LucideIcons.pencil, onTap: widget.onEdit, isDark: isDark),
                    const SizedBox(width: 4),
                    _CardActionBtn(icon: LucideIcons.trash2, onTap: widget.onDelete, isDark: isDark, isDestructive: true),
                  ],
                ),
              ),
            ),
          ),
        ],
      ),
    );
  }
}

class _CardActionBtn extends StatelessWidget {
  final IconData icon;
  final VoidCallback onTap;
  final bool isDark;
  final bool isDestructive;

  const _CardActionBtn({
    required this.icon,
    required this.onTap,
    required this.isDark,
    this.isDestructive = false,
  });

  @override
  Widget build(BuildContext context) {
    return Material(
      color: isDark ? AppColors.darkSurface : Colors.white,
      borderRadius: BorderRadius.circular(7),
      child: InkWell(
        borderRadius: BorderRadius.circular(7),
        onTap: onTap,
        hoverColor: isDestructive
            ? (isDark ? AppColors.errorBgDarkMode : AppColors.errorBg)
            : (isDark ? AppColors.darkSurfaceMuted : AppColors.lightSurfaceMuted),
        child: Padding(
          padding: const EdgeInsets.all(5),
          child: Icon(
            icon,
            size: 14,
            color: isDestructive ? AppColors.error : (isDark ? AppColors.darkTextTertiary : AppColors.lightTextTertiary),
          ),
        ),
      ),
    );
  }
}
