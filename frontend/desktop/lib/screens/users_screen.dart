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
import 'widgets/delete_organization_admin_dialog.dart';
import 'widgets/organization_admin_upsert_dialog.dart';
import 'widgets/pagination_bar.dart';
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

  void _openAddDialog() {
    showDialog(
      context: context,
      barrierDismissible: false,
      builder: (_) => OrganizationAdminUpsertDialog(
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
        ScaffoldMessenger.of(context).showSnackBar(
          SnackBar(
            content: Row(
              children: [
                const Icon(Icons.check_circle_outline, color: Colors.white),
                const SizedBox(width: 10),
                Expanded(child: Text('Korisnik "${user.fullName}" je uspješno obrisan')),
              ],
            ),
            backgroundColor: AppColors.primary,
            behavior: SnackBarBehavior.floating,
            shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(10)),
            margin: const EdgeInsets.all(16),
            duration: const Duration(seconds: 3),
          ),
        );
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
    // The whole page is one SingleChildScrollView (not header-fixed +
    // internally-scrolling grid) — scrolling moves the title/stats/search out
    // of view along with everything else, so the pagination bar is always
    // reachable by scrolling the same way as the rest of the content.
    return SingleChildScrollView(
      padding: const EdgeInsets.all(24),
      child: Column(
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
              if (!_isSuperAdmin) ...[
                const SizedBox(width: 16),
                _AddAdminButton(onTap: _openAddDialog),
              ],
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
              // stretching. mainAxisExtent (a fixed pixel height, not an aspect
              // ratio) is generous enough for the avatar, name, up to 3 badges,
              // 3 contact lines and 2 action buttons, so it can never overflow.
              shrinkWrap: true,
              physics: const NeverScrollableScrollPhysics(),
              gridDelegate: const SliverGridDelegateWithMaxCrossAxisExtent(
                maxCrossAxisExtent: 260,
                mainAxisExtent: 380,
                crossAxisSpacing: 14,
                mainAxisSpacing: 14,
              ),
              itemCount: _users.length,
              itemBuilder: (context, index) {
                final user = _users[index];
                final isSelf = user.id == widget.currentUser.id;
                // SuperAdmin can set the password of any Admin/Organization*
                // Admin account except their own and another SuperAdmin's
                // (see AdminService.SetPasswordAsync's role exclusions).
                final canSetPassword = _isSuperAdmin && !isSelf && user.roleName != 'SuperAdmin';
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

// ─────────────────────────────────────────────────────────────────────────────
//  Add administrator button (org self-service view only)
// ─────────────────────────────────────────────────────────────────────────────

class _AddAdminButton extends StatelessWidget {
  final VoidCallback onTap;

  const _AddAdminButton({required this.onTap});

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
                Text(
                  'Dodaj Administratora',
                  style: TextStyle(color: onPrimaryColor, fontWeight: FontWeight.w600, fontSize: 14),
                ),
              ],
            ),
          ),
        ),
      ),
    );
  }
}
