import 'dart:async';

import 'package:flutter/material.dart';
import 'package:lucide_icons_flutter/lucide_icons.dart';
import '../models/requests/delete_admin_request.dart';
import '../models/responses/admin_user_response.dart';
import '../models/responses/user_profile.dart';
import '../models/search_objects/organization_user_search_object.dart';
import '../models/search_objects/staff_query_search_object.dart';
import '../providers/user_provider.dart';
import '../theme/app_colors.dart';
import '../widgets/confirm_dialog.dart';
import 'widgets/admin_upsert_dialog.dart';
import 'widgets/delete_organization_admin_dialog.dart';
import 'widgets/organization_admin_upsert_dialog.dart';
import 'widgets/pagination_bar.dart';
import 'widgets/paginated_screen_body.dart';
import 'widgets/role_multi_select_filter.dart';
import 'widgets/set_staff_password_dialog.dart';
import 'widgets/staff_user_upsert_dialog.dart';
import 'widgets/stat_card.dart';
import 'widgets/user_grid_card.dart';
import '../main.dart';

class UsersScreen extends StatefulWidget {
  final UserProfile currentUser;

  const UsersScreen({super.key, required this.currentUser});

  @override
  State<UsersScreen> createState() => _UsersScreenState();
}

class _UsersScreenState extends State<UsersScreen> {
  final _searchController = TextEditingController();
  Timer? _debounce;

  List<AdminUserResponse> _users = [];
  bool _isLoading = true;

  int _currentPage = 0;
  int _totalCount = 0;
  int _pageSize = 10;

  /// SuperAdmin-only role filter — a set of RoleType enum member names, empty = every role.
  List<String> _selectedRoles = [];

  /// The org self-service Users tab always scopes to this role server-side, so
  /// the caller (an OrganizationSuperAdmin) never sees themselves or any other
  /// organization's staff in the list.
  static const String _organizationAdminRole = 'OrganizationAdmin';

  int get _totalPages => (_totalCount / _pageSize).ceil().clamp(1, 99999);

  bool get _isSuperAdmin => widget.currentUser.roleName == 'SuperAdmin';

  /// Whether this account gets the extra, full-width "Promijeni Lozinku" button on its card.
  /// SuperAdmin can set the password of any Admin/Organization*Admin account except their own
  /// and another SuperAdmin's (see AdminService.SetPasswordAsync's role exclusions).
  ///
  /// Single definition on purpose: the grid sizes its cells from it (a card with the third button
  /// is taller) and the item builder wires the callback from it. When those two drifted apart the
  /// cell was 42px too short and every SuperAdmin card threw a RenderFlex overflow.
  bool _canSetPassword(AdminUserResponse user) =>
      _isSuperAdmin && user.id != widget.currentUser.id && user.roleName != 'SuperAdmin';

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

  void _onRolesFilterChanged(List<String> roles) {
    setState(() {
      _selectedRoles = roles;
      _currentPage = 0;
    });
    _loadData();
  }

  void _onPageSizeChanged(int size) {
    setState(() {
      _pageSize = size;
      _currentPage = 0;
    });
    _loadData();
  }

  Future<void> _loadData() async {
    setState(() => _isLoading = true);
    try {
      final fts = _searchController.text.trim().isEmpty ? null : _searchController.text.trim();

      // SuperAdmin    → GET /api/admins (all non-User roles, optional multi-role filter)
      // OrgSuperAdmin → GET /api/organizations/{organizationId}/users (Role=OrganizationAdmin only)
      final result = _isSuperAdmin
          ? await AdminProvider().getAll(
              searchObject: StaffQuerySearchObject(
                page: _currentPage,
                pageSize: _pageSize,
                fts: fts,
                roleFilters: _selectedRoles,
              ),
              fromJson: AdminUserResponse.fromJson,
            )
          : await OrganizationUsersProvider().getAll(
              organizationId: widget.currentUser.organizationId!,
              searchObject: OrganizationUserSearchObject(
                page: _currentPage,
                pageSize: _pageSize,
                fts: fts,
                role: _organizationAdminRole,
              ),
              fromJson: AdminUserResponse.fromJson,
            );

      if (mounted) {
        setState(() {
          _users = result.items;
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

  void _goToPage(int page) {
    if (page < 0 || page >= _totalPages) return;
    setState(() => _currentPage = page);
    _loadData();
  }

  void _openEditDialog(AdminUserResponse user) {
    if (_isSuperAdmin) {
      showDialog(
        context: context,
        barrierDismissible: false,
        builder: (_) => StaffUserUpsertDialog(user: user, onSaved: _loadData),
      );
    } else {
      showDialog(
        context: context,
        barrierDismissible: false,
        builder: (_) => OrganizationAdminUpsertDialog(
          organizationId: widget.currentUser.organizationId!,
          user: user,
          onSaved: _loadData,
        ),
      );
    }
  }

  void _openSetPasswordDialog(AdminUserResponse user) {
    showDialog(
      context: context,
      barrierDismissible: false,
      builder: (_) => SetStaffPasswordDialog(
        userId: user.id,
        userFullName: user.fullName,
        onSaved: _loadData,
      ),
    );
  }

  /// The two roles that reach this screen add two different kinds of account, so the button
  /// routes to two different dialogs: a SuperAdmin creates a platform `Admin` (POST /admins),
  /// an OrganizationSuperAdmin an `OrganizationAdmin` in their own org
  /// (POST /organizations/{id}/users). Both roles are pinned server-side, not chosen here.
  void _openAddDialog() {
    showDialog(
      context: context,
      barrierDismissible: false,
      builder: (_) => _isSuperAdmin
          ? AdminUpsertDialog(onSaved: _loadData)
          : OrganizationAdminUpsertDialog(
              organizationId: widget.currentUser.organizationId!,
              onSaved: _loadData,
            ),
    );
  }

  Future<void> _deleteUser(AdminUserResponse user) async {
    // SuperAdmin deleting an OrganizationAdmin is the one case the backend
    // requires a reason + a notification recipient for (see
    // AdminService.DeleteAsync) — everything else keeps the plain confirm.
    final requiresReason = _isSuperAdmin && user.roleName == 'OrganizationAdmin';

    DeleteAdminRequest? deleteRequest;
    if (requiresReason) {
      deleteRequest = await showDialog<DeleteAdminRequest>(
        context: context,
        barrierDismissible: false,
        builder: (_) => DeleteOrganizationAdminDialog(userFullName: user.fullName),
      );
      if (deleteRequest == null || !mounted) return;
    } else {
      final confirmed = await ConfirmDialog.show(
        context,
        title: 'Obriši korisnika',
        message: 'Da li ste sigurni da želite obrisati korisnika "${user.fullName}"?',
        confirmLabel: 'Obriši',
      );

      if (confirmed != true || !mounted) return;
    }

    try {
      if (_isSuperAdmin) {
        // The backend's DELETE /admins/{id} body is required now (even for
        // non-OrganizationAdmin targets, where it's just {}) — see
        // AdminEndpoints.Delete.
        await AdminProvider().delete(user.id, data: (deleteRequest ?? const DeleteAdminRequest()).toJson());
      } else {
        await OrganizationUsersProvider().delete(widget.currentUser.organizationId!, user.id);
      }
      if (!mounted) return;

      if (_users.length == 1 && _currentPage > 0) {
        setState(() => _currentPage--);
      }
      await _loadData();

      if (mounted) {
        handleApiSuccess('Korisnik "${user.fullName}" je uspješno obrisan.');
      }
    } catch (e) {
      if (mounted) handleApiError(e);
    }
  }

  @override
  void dispose() {
    _debounce?.cancel();
    _searchController.dispose();
    super.dispose();
  }

  int get _activeCount => _users.where((u) => u.isActive).length;

  int get _adminCount =>
      _users.where((u) => u.roleName.contains('Admin')).length;

  @override
  Widget build(BuildContext context) {
    final isDark = Theme.of(context).brightness == Brightness.dark;
    // The pagination bar is pinned to the bottom of the viewport (see
    // PaginatedScreenBody) — only the header/stats/search/grid above it scroll.
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
          // ── Header ─────────────────────────────────────────────────
          Row(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              Expanded(
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    Text(
                      _isSuperAdmin ? 'Korisnici Platforme' : 'Korisnici Organizacije',
                      style: TextStyle(
                        fontSize: 30,
                        fontWeight: FontWeight.w700,
                        color: isDark ? AppColors.darkTextPrimary : AppColors.lightTextPrimary,
                        height: 1.2,
                      ),
                    ),
                    const SizedBox(height: 8),
                    Text(
                      _isSuperAdmin
                          ? 'Upravljajte korisnicima kroz sve organizacije'
                          : 'Upravljajte administratorima u ${widget.currentUser.organizationName ?? 'vašoj organizaciji'}',
                      style: TextStyle(
                          fontSize: 16,
                          color: isDark ? AppColors.darkTextSecondary : AppColors.lightTextSecondary),
                    ),
                  ],
                ),
              ),
              // Both roles that reach this screen can create an account; _openAddDialog picks
              // which kind. (It used to be hidden for SuperAdmin, which left a platform Admin
              // impossible to create anywhere in the app.)
              const SizedBox(width: 16),
              _AddAdminButton(
                onTap: _openAddDialog,
                label: _isSuperAdmin ? 'Dodaj Administratora Platforme' : 'Dodaj Administratora',
              ),
            ],
          ),

          const SizedBox(height: 24),

          // ── Stats Cards ────────────────────────────────────────────
          if (!_isLoading)
            Row(
              children: [
                StatCard(
                  label: 'Ukupno Korisnika',
                  value: '$_totalCount',
                  valueColor: isDark ? AppColors.darkTextPrimary : AppColors.lightTextPrimary,
                ),
                const SizedBox(width: 16),
                StatCard(
                  label: 'Aktivni (Prikazano)',
                  value: '$_activeCount',
                  valueColor: isDark ? AppColors.secondary : AppColors.primary,
                ),
                const SizedBox(width: 16),
                StatCard(
                  label: 'Admini (Prikazano)',
                  value: '$_adminCount',
                  valueColor: isDark ? AppColors.darkTextPrimary : AppColors.lightTextPrimary,
                ),
              ],
            ),

          const SizedBox(height: 24),

          // ── Search ─────────────────────────────────────────────────
          Container(
            height: 46,
            decoration: BoxDecoration(
              color: isDark ? AppColors.darkInputFill : Colors.white,
              border: Border.all(
                  color: isDark ? AppColors.darkBorderInput : AppColors.lightBorderInput),
              borderRadius: BorderRadius.circular(12),
            ),
            child: TextField(
              controller: _searchController,
              onChanged: _onSearchChanged,
              decoration: InputDecoration(
                hintText: 'Pretražite korisnike po imenu, emailu ili ulozi...',
                hintStyle: TextStyle(
                    color: isDark ? AppColors.darkTextTertiary : AppColors.lightTextTertiary,
                    fontSize: 14),
                prefixIcon: Icon(LucideIcons.search,
                    color: isDark ? AppColors.darkTextDisabled : AppColors.lightTextDisabled,
                    size: 18),
                border: InputBorder.none,
                contentPadding: const EdgeInsets.symmetric(vertical: 13),
              ),
              style: TextStyle(
                  color: isDark ? AppColors.darkTextPrimary : AppColors.lightTextPrimary),
            ),
          ),

          // ── Role multiselect filter (SuperAdmin only) ─────────────
          if (_isSuperAdmin) ...[
            const SizedBox(height: 12),
            RoleMultiSelectFilter(selectedRoles: _selectedRoles, onChanged: _onRolesFilterChanged),
          ],

          const SizedBox(height: 16),

          // ── User Cards ── not wrapped in Expanded (the page is one scroll
          // view, not header-fixed + internally-scrolling grid) — loading/empty
          // states get an explicit height since there's no ambient Expanded to
          // size them anymore.
          if (_isLoading)
            SizedBox(
              height: 300,
              child: Center(
                child: CircularProgressIndicator(
                    color: isDark ? AppColors.secondary : AppColors.primary),
              ),
            )
          else if (_users.isEmpty)
            SizedBox(
              height: 300,
              child: Center(
                child: Column(
                  mainAxisSize: MainAxisSize.min,
                  children: [
                    Icon(LucideIcons.userX,
                        size: 48,
                        color: isDark ? AppColors.darkBorderInput : AppColors.lightBorderInput),
                    const SizedBox(height: 12),
                    Text(
                      'Nema korisnika',
                      style: TextStyle(
                          color: isDark ? AppColors.darkTextTertiary : AppColors.lightTextTertiary,
                          fontSize: 16),
                    ),
                  ],
                ),
              ),
            )
          else
            GridView.builder(
              // MaxCrossAxisExtent (not a breakpoint-driven fixed count) caps how
              // wide/tall any single card can get — a fixed crossAxisCount let a
              // card grow arbitrarily large on a wide monitor with few results,
              // which looked broken even though nothing overflowed. This way more
              // columns appear as the window widens instead of existing cards
              // stretching.
              //
              // mainAxisExtent is a fixed pixel height, so it has to cover the tallest card on
              // the page: avatar, name, up to 3 badges, 3 contact lines, and either two action
              // buttons (280) or those plus the stacked "Promijeni Lozinku" button (322). One
              // height for the whole grid, driven by whether any card on this page shows it —
              // rather than 322 for everyone, which would leave a gap under every other card.
              shrinkWrap: true,
              physics: const NeverScrollableScrollPhysics(),
              gridDelegate: SliverGridDelegateWithMaxCrossAxisExtent(
                maxCrossAxisExtent: 260,
                mainAxisExtent: _users.any(_canSetPassword) ? 322 : 280,
                crossAxisSpacing: 14,
                mainAxisSpacing: 14,
              ),
              itemCount: _users.length,
              itemBuilder: (context, index) {
                final user = _users[index];
                final isSelf = user.id == widget.currentUser.id;
                final canSetPassword = _canSetPassword(user);
                return UserGridCard(
                  user: user,
                  // A user never edits/deletes their own account from this
                  // screen — avoids an accidental self-lockout (self-service
                  // profile editing already exists via the settings dialog).
                  onEdit: isSelf ? null : () => _openEditDialog(user),
                  onDelete: isSelf ? null : () => _deleteUser(user),
                  onSetPassword: canSetPassword ? () => _openSetPasswordDialog(user) : null,
                );
              },
            ),

        ],
      ),
    );
  }
}

// ─────────────────────────────────────────────────────────────────────────────
//  Add administrator button (org self-service view only)
// ─────────────────────────────────────────────────────────────────────────────

class _AddAdminButton extends StatelessWidget {
  final VoidCallback onTap;

  /// Named by the caller: the two roles on this screen create different kinds of account, and
  /// a SuperAdmin's button says so rather than implying it adds an organization admin.
  final String label;

  const _AddAdminButton({required this.onTap, required this.label});

  @override
  Widget build(BuildContext context) {
    final isDark = Theme.of(context).brightness == Brightness.dark;
    final primary = isDark ? AppColors.secondary : AppColors.primary;
    final primaryDark = isDark ? AppColors.primary : AppColors.primaryDark;
    final onPrimaryColor = isDark ? AppColors.darkBackground : Colors.white;

    return Container(
      decoration: BoxDecoration(
        gradient: LinearGradient(colors: [primary, primaryDark]),
        borderRadius: BorderRadius.circular(12),
        boxShadow: [
          BoxShadow(color: primary.withValues(alpha: 0.2), blurRadius: 8, offset: const Offset(0, 3)),
        ],
      ),
      child: Material(
        color: Colors.transparent,
        child: InkWell(
          borderRadius: BorderRadius.circular(12),
          onTap: onTap,
          child: Padding(
            padding: const EdgeInsets.symmetric(horizontal: 16, vertical: 12),
            child: Row(
              mainAxisSize: MainAxisSize.min,
              children: [
                Icon(LucideIcons.userPlus, color: onPrimaryColor, size: 18),
                const SizedBox(width: 8),
                Flexible(
                  child: Text(
                    label,
                    maxLines: 1,
                    overflow: TextOverflow.ellipsis,
                    style: TextStyle(color: onPrimaryColor, fontWeight: FontWeight.w600, fontSize: 14),
                  ),
                ),
              ],
            ),
          ),
        ),
      ),
    );
  }
}
