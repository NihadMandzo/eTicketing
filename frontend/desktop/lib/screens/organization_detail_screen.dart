import 'dart:async';

import 'package:flutter/material.dart';
import 'package:lucide_icons_flutter/lucide_icons.dart';

import '../models/responses/admin_user_response.dart';
import '../models/responses/event_response.dart';
import '../models/responses/organization_response.dart';
import '../models/search_objects/base_search_object.dart';
import '../models/search_objects/organization_user_search_object.dart';
import '../providers/organization_events_provider.dart';
import '../providers/organization_provider.dart';
import '../providers/user_provider.dart';
import '../theme/app_colors.dart';
import '../theme/role_badge.dart';
import 'widgets/entity_avatar.dart';
import 'widgets/organization_upsert_dialog.dart';
import 'widgets/pagination_bar.dart';
import 'widgets/stat_card.dart';
import '../main.dart';

/// Per-tab paginated/searchable list state — instantiated once per tab
/// (Events / Admins / SuperAdmins) so each tab paginates and searches fully
/// independently, mirroring the single-list state shape used by
/// UsersScreen/OrganizationsScreen but generic over 3 simultaneous lists.
class _TabState<T> {
  List<T> items = [];
  int totalCount = 0;
  int currentPage = 0;
  bool isLoading = true;
  final searchController = TextEditingController();
  Timer? debounce;

  int totalPages(int pageSize) => (totalCount / pageSize).ceil().clamp(1, 99999);

  void dispose() {
    debounce?.cancel();
    searchController.dispose();
  }
}

/// Superadmin-facing organization detail screen: header + event/admin/
/// superadmin counts + independently paginated/searchable sub-lists.
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
  static const int _pageSize = 10;

  late TabController _tabController;
  late OrganizationResponse _organization;

  final _eventsState = _TabState<EventResponse>();
  final _adminsState = _TabState<AdminUserResponse>();
  final _superAdminsState = _TabState<AdminUserResponse>();

  bool get _isDark => Theme.of(context).brightness == Brightness.dark;

  @override
  void initState() {
    super.initState();
    _organization = widget.organization;
    _tabController = TabController(length: 3, vsync: this);
    Future.wait([_loadEvents(), _loadAdmins(), _loadSuperAdmins()]);
  }

  @override
  void dispose() {
    _tabController.dispose();
    _eventsState.dispose();
    _adminsState.dispose();
    _superAdminsState.dispose();
    super.dispose();
  }

  // ── Data loading ─────────────────────────────────────────────────────────

  Future<void> _loadEvents() async {
    setState(() => _eventsState.isLoading = true);
    try {
      final searchObject = BaseSearchObject(
        page: _eventsState.currentPage,
        pageSize: _pageSize,
        fts: _eventsState.searchController.text.trim().isEmpty
            ? null
            : _eventsState.searchController.text.trim(),
      );
      final result = await OrganizationEventsProvider().getAll(
        organizationId: _organization.id,
        searchObject: searchObject,
        fromJson: EventResponse.fromJson,
      );
      if (mounted) {
        setState(() {
          _eventsState.items = result.items;
          _eventsState.totalCount = result.totalCount;
          _eventsState.isLoading = false;
        });
      }
    } catch (e) {
      if (mounted) {
        setState(() => _eventsState.isLoading = false);
        handleApiError(e);
      }
    }
  }

  Future<void> _loadAdmins() => _loadOrgUsers(_adminsState, 'OrganizationAdmin');

  Future<void> _loadSuperAdmins() => _loadOrgUsers(_superAdminsState, 'OrganizationSuperAdmin');

  Future<void> _loadOrgUsers(_TabState<AdminUserResponse> state, String role) async {
    setState(() => state.isLoading = true);
    try {
      final searchObject = OrganizationUserSearchObject(
        page: state.currentPage,
        pageSize: _pageSize,
        fts: state.searchController.text.trim().isEmpty
            ? null
            : state.searchController.text.trim(),
        role: role,
      );
      final result = await OrganizationUsersProvider().getAll(
        organizationId: _organization.id,
        searchObject: searchObject,
        fromJson: AdminUserResponse.fromJson,
      );
      if (mounted) {
        setState(() {
          state.items = result.items;
          state.totalCount = result.totalCount;
          state.isLoading = false;
        });
      }
    } catch (e) {
      if (mounted) {
        setState(() => state.isLoading = false);
        handleApiError(e);
      }
    }
  }

  void _onSearchChanged(_TabState state, Future<void> Function() reload) {
    if (state.debounce?.isActive ?? false) state.debounce!.cancel();
    state.debounce = Timer(const Duration(milliseconds: 300), () {
      setState(() => state.currentPage = 0);
      reload();
    });
  }

  void _goToPage(_TabState state, int page, Future<void> Function() reload) {
    if (page < 0 || page >= state.totalPages(_pageSize)) return;
    setState(() => state.currentPage = page);
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
      body: Padding(
        padding: const EdgeInsets.all(24),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            _buildHeader(isDark, textPrimary, textTertiary),
            const SizedBox(height: 24),
            Row(
              children: [
                StatCard(
                  label: 'Broj Događaja',
                  value: '${_eventsState.totalCount}',
                  valueColor: textPrimary,
                ),
                const SizedBox(width: 16),
                StatCard(
                  label: 'Broj Administratora',
                  value: '${_adminsState.totalCount}',
                  valueColor: primary,
                ),
                const SizedBox(width: 16),
                StatCard(
                  label: 'Broj Super Administratora',
                  value: '${_superAdminsState.totalCount}',
                  valueColor: AppColors.rolePurple,
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
              tabs: const [
                Tab(text: 'Događaji'),
                Tab(text: 'Administratori'),
                Tab(text: 'Super Administratori'),
              ],
            ),
            Expanded(
              child: TabBarView(
                controller: _tabController,
                children: [
                  _buildEventsTab(isDark, textPrimary, textTertiary),
                  _buildAdminsTab(_adminsState, _loadAdmins, isDark, textPrimary, textTertiary),
                  _buildAdminsTab(
                      _superAdminsState, _loadSuperAdmins, isDark, textPrimary, textTertiary),
                ],
              ),
            ),
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
                    Text(
                      _organization.name,
                      style: TextStyle(fontSize: 20, fontWeight: FontWeight.w700, color: textPrimary),
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

  Widget _buildEventsTab(bool isDark, Color textPrimary, Color textTertiary) {
    return Padding(
      padding: const EdgeInsets.only(top: 16),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          _searchField(_eventsState, _loadEvents, 'Pretražite događaje...', isDark, textPrimary, textTertiary),
          const SizedBox(height: 16),
          Expanded(
            child: _eventsState.isLoading
                ? Center(child: CircularProgressIndicator(color: isDark ? AppColors.secondary : AppColors.primary))
                : _eventsState.items.isEmpty
                    ? _emptyState(LucideIcons.calendarX, 'Nema događaja', isDark, textTertiary)
                    : ListView.separated(
                        itemCount: _eventsState.items.length,
                        separatorBuilder: (_, _) => const SizedBox(height: 12),
                        itemBuilder: (context, index) =>
                            _EventCard(event: _eventsState.items[index], isDark: isDark),
                      ),
          ),
          Padding(
            padding: const EdgeInsets.only(top: 16),
            child: PaginationBar(
              currentPage: _eventsState.currentPage,
              totalPages: _eventsState.totalPages(_pageSize),
              onPageChanged: (page) => _goToPage(_eventsState, page, _loadEvents),
            ),
          ),
        ],
      ),
    );
  }

  Widget _buildAdminsTab(_TabState<AdminUserResponse> state, Future<void> Function() reload,
      bool isDark, Color textPrimary, Color textTertiary) {
    return Padding(
      padding: const EdgeInsets.only(top: 16),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          _searchField(state, reload, 'Pretražite po imenu ili emailu...', isDark, textPrimary, textTertiary),
          const SizedBox(height: 16),
          Expanded(
            child: state.isLoading
                ? Center(child: CircularProgressIndicator(color: isDark ? AppColors.secondary : AppColors.primary))
                : state.items.isEmpty
                    ? _emptyState(LucideIcons.userX, 'Nema korisnika', isDark, textTertiary)
                    : ListView.separated(
                        itemCount: state.items.length,
                        separatorBuilder: (_, _) => const SizedBox(height: 12),
                        itemBuilder: (context, index) =>
                            _AdminRow(user: state.items[index], isDark: isDark),
                      ),
          ),
          Padding(
            padding: const EdgeInsets.only(top: 16),
            child: PaginationBar(
              currentPage: state.currentPage,
              totalPages: state.totalPages(_pageSize),
              onPageChanged: (page) => _goToPage(state, page, reload),
            ),
          ),
        ],
      ),
    );
  }

  Widget _searchField(_TabState state, Future<void> Function() reload, String hint, bool isDark,
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

// ─── Event card ─────────────────────────────────────────────────────────────

class _EventCard extends StatelessWidget {
  final EventResponse event;
  final bool isDark;

  const _EventCard({required this.event, required this.isDark});

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
    final textPrimary = isDark ? AppColors.darkTextPrimary : AppColors.lightTextPrimary;
    final textTertiary = isDark ? AppColors.darkTextTertiary : AppColors.lightTextTertiary;
    final statusColor = event.isPublished ? AppColors.success : AppColors.warning;
    return Container(
      padding: const EdgeInsets.all(16),
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
                Text(event.name,
                    style: TextStyle(fontSize: 15, fontWeight: FontWeight.w700, color: textPrimary)),
                const SizedBox(height: 4),
                Text(_formatDate(event.date), style: TextStyle(fontSize: 13, color: textTertiary)),
              ],
            ),
          ),
          Container(
            padding: const EdgeInsets.symmetric(horizontal: 10, vertical: 4),
            decoration: BoxDecoration(
              color: (isDark ? AppColors.secondary : AppColors.primary).withValues(alpha: isDark ? 0.18 : 0.1),
              borderRadius: BorderRadius.circular(20),
            ),
            child: Text(
              event.categoryName,
              style: TextStyle(
                fontSize: 12,
                fontWeight: FontWeight.w600,
                color: isDark ? AppColors.secondary : AppColors.primary,
              ),
            ),
          ),
          const SizedBox(width: 8),
          Container(
            padding: const EdgeInsets.symmetric(horizontal: 10, vertical: 4),
            decoration: BoxDecoration(
              color: statusColor.withValues(alpha: isDark ? 0.18 : 0.1),
              borderRadius: BorderRadius.circular(20),
            ),
            child: Text(
              event.isPublished ? 'Objavljen' : 'Nacrt',
              style: TextStyle(
                fontSize: 12,
                fontWeight: FontWeight.w600,
                color: event.isPublished ? AppColors.successDark : AppColors.warningDark,
              ),
            ),
          ),
        ],
      ),
    );
  }
}

// ─── Admin/superadmin row ───────────────────────────────────────────────────

class _AdminRow extends StatelessWidget {
  final AdminUserResponse user;
  final bool isDark;

  const _AdminRow({required this.user, required this.isDark});

  String _initials(String name) {
    if (name.trim().isEmpty) return '?';
    return name
        .trim()
        .split(RegExp(r'\s+'))
        .take(2)
        .map((w) => w.isNotEmpty ? w[0].toUpperCase() : '')
        .join();
  }

  @override
  Widget build(BuildContext context) {
    final textPrimary = isDark ? AppColors.darkTextPrimary : AppColors.lightTextPrimary;
    final textTertiary = isDark ? AppColors.darkTextTertiary : AppColors.lightTextTertiary;
    final roleColor = roleBadgeColor(user.roleName, isDark);
    return Container(
      padding: const EdgeInsets.all(16),
      decoration: BoxDecoration(
        color: isDark ? AppColors.darkSurface : Colors.white,
        borderRadius: BorderRadius.circular(14),
        border: Border.all(color: isDark ? AppColors.darkBorder : AppColors.lightBorder),
      ),
      child: Row(
        children: [
          Container(
            width: 44,
            height: 44,
            decoration: BoxDecoration(
              gradient: LinearGradient(
                colors: isDark
                    ? [AppColors.secondary, AppColors.primary]
                    : [AppColors.primary, AppColors.primaryDark],
              ),
              borderRadius: BorderRadius.circular(12),
            ),
            alignment: Alignment.center,
            child: Text(
              _initials(user.fullName),
              style: TextStyle(
                color: isDark ? AppColors.darkBackground : Colors.white,
                fontWeight: FontWeight.w700,
              ),
            ),
          ),
          const SizedBox(width: 14),
          Expanded(
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Text(user.fullName,
                    style: TextStyle(fontSize: 15, fontWeight: FontWeight.w700, color: textPrimary)),
                const SizedBox(height: 2),
                Text(user.email, style: TextStyle(fontSize: 13, color: textTertiary)),
              ],
            ),
          ),
          Container(
            padding: const EdgeInsets.symmetric(horizontal: 10, vertical: 4),
            decoration: BoxDecoration(
              color: roleColor.withValues(alpha: isDark ? 0.18 : 0.1),
              borderRadius: BorderRadius.circular(20),
            ),
            child: Text(
              roleBadgeText(user.roleName),
              style: TextStyle(fontSize: 12, fontWeight: FontWeight.w600, color: roleColor),
            ),
          ),
          const SizedBox(width: 8),
          Container(
            padding: const EdgeInsets.symmetric(horizontal: 10, vertical: 4),
            decoration: BoxDecoration(
              color: (user.isActive ? AppColors.success : AppColors.error)
                  .withValues(alpha: isDark ? 0.18 : 0.1),
              borderRadius: BorderRadius.circular(20),
            ),
            child: Text(
              user.isActive ? 'Aktivan' : 'Neaktivan',
              style: TextStyle(
                fontSize: 12,
                fontWeight: FontWeight.w600,
                color: user.isActive ? AppColors.successDark : AppColors.errorDark,
              ),
            ),
          ),
        ],
      ),
    );
  }
}
