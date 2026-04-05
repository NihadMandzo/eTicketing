import 'dart:async';

import 'package:flutter/material.dart';
import 'package:lucide_icons_flutter/lucide_icons.dart';
import '../models/responses/admin_user_response.dart';
import '../models/responses/user_profile.dart';
import '../models/search_objects/base_search_object.dart';
import '../providers/user_provider.dart';
import 'widgets/pagination_bar.dart';
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
  static const int _pageSize = 10;

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

  Future<void> _loadData() async {
    setState(() => _isLoading = true);
    try {
      final searchObject = BaseSearchObject(
        page: _currentPage,
        pageSize: _pageSize,
        fts: _searchController.text.trim().isEmpty
            ? null
            : _searchController.text.trim(),
      );

      // SuperAdmin  → GET /api/Admin
      // OrgSuperAdmin → GET /api/Organizations/users
      final provider =
          _isSuperAdmin ? AdminProvider() : OrganizationUsersProvider();

      final result = await provider.getAll(
        searchObject: searchObject,
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

  @override
  void dispose() {
    _debounce?.cancel();
    _searchController.dispose();
    super.dispose();
  }

  // ── Helpers ─────────────────────────────────────────────────────────────────

  String _formatDate(DateTime? date) {
    if (date == null) return '—';
    final d = date.toLocal();
    final months = [
      '', 'jan', 'feb', 'mar', 'apr', 'maj', 'jun',
      'jul', 'aug', 'sep', 'okt', 'nov', 'dec',
    ];
    return '${d.day}. ${months[d.month]} ${d.year}.';
  }

  String _formatDateShort(DateTime? date) {
    if (date == null) return '—';
    final d = date.toLocal();
    final months = [
      '', 'jan', 'feb', 'mar', 'apr', 'maj', 'jun',
      'jul', 'aug', 'sep', 'okt', 'nov', 'dec',
    ];
    return '${d.day}. ${months[d.month]}';
  }

  String _roleBadgeText(String role) {
    switch (role) {
      case 'SuperAdmin':
        return 'Super Admin';
      case 'Admin':
        return 'Admin';
      case 'OrganizationSuperAdmin':
        return 'Org Super Admin';
      case 'OrganizationAdmin':
        return 'Org Admin';
      case 'User':
        return 'Korisnik';
      default:
        return role;
    }
  }

  Color _roleBadgeColor(String role) {
    switch (role) {
      case 'SuperAdmin':
        return const Color(0xFF7C3AED);
      case 'Admin':
        return const Color(0xFF2563EB);
      case 'OrganizationSuperAdmin':
        return const Color(0xFF0D7C66);
      case 'OrganizationAdmin':
        return const Color(0xFF0891B2);
      case 'User':
        return const Color(0xFF6B7280);
      default:
        return const Color(0xFF6B7280);
    }
  }

  int get _activeCount => _users.where((u) => u.isActive).length;

  int get _adminCount =>
      _users.where((u) => u.roleName.contains('Admin')).length;

  @override
  Widget build(BuildContext context) {
    return Padding(
      padding: const EdgeInsets.all(24),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          // ── Header ─────────────────────────────────────────────────
          Column(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              const Text(
                'Korisnici Organizacije',
                style: TextStyle(
                  fontSize: 30,
                  fontWeight: FontWeight.w700,
                  color: Color(0xFF111827),
                  height: 1.2,
                ),
              ),
              const SizedBox(height: 8),
              Text(
                _isSuperAdmin
                    ? 'Upravljajte korisnicima kroz sve organizacije'
                    : 'Upravljajte korisnicima u ${widget.currentUser.organizationName ?? 'vašoj organizaciji'}',
                style:
                    const TextStyle(fontSize: 16, color: Color(0xFF4B5563)),
              ),
            ],
          ),

          const SizedBox(height: 24),

          // ── Stats Cards ────────────────────────────────────────────
          if (!_isLoading)
            Row(
              children: [
                _StatCard(
                  label: 'Ukupno Korisnika',
                  value: '$_totalCount',
                  valueColor: const Color(0xFF111827),
                ),
                const SizedBox(width: 16),
                _StatCard(
                  label: 'Aktivni (Prikazano)',
                  value: '$_activeCount',
                  valueColor: const Color(0xFF0D7C66),
                ),
                const SizedBox(width: 16),
                _StatCard(
                  label: 'Admini (Prikazano)',
                  value: '$_adminCount',
                  valueColor: const Color(0xFF111827),
                ),
              ],
            ),

          const SizedBox(height: 24),

          // ── Search ─────────────────────────────────────────────────
          Container(
            height: 46,
            decoration: BoxDecoration(
              color: Colors.white,
              border: Border.all(color: const Color(0xFFD1D5DB)),
              borderRadius: BorderRadius.circular(12),
            ),
            child: TextField(
              controller: _searchController,
              onChanged: _onSearchChanged,
              decoration: const InputDecoration(
                hintText: 'Pretražite korisnike po imenu, emailu ili ulozi...',
                hintStyle: TextStyle(color: Color(0xFF6B7280), fontSize: 14),
                prefixIcon: Icon(LucideIcons.search,
                    color: Color(0xFF9CA3AF), size: 18),
                border: InputBorder.none,
                contentPadding: EdgeInsets.symmetric(vertical: 13),
              ),
              style: const TextStyle(color: Color(0xFF111827)),
            ),
          ),

          const SizedBox(height: 16),

          // ── User Cards ─────────────────────────────────────────────
          Expanded(
            child: _isLoading
                ? const Center(
                    child: CircularProgressIndicator(
                        color: Color(0xFF0D7C66)),
                  )
                : _users.isEmpty
                    ? Center(
                        child: Column(
                          mainAxisSize: MainAxisSize.min,
                          children: const [
                            Icon(LucideIcons.userX,
                                size: 48, color: Color(0xFFD1D5DB)),
                            SizedBox(height: 12),
                            Text(
                              'Nema korisnika',
                              style: TextStyle(
                                  color: Color(0xFF6B7280), fontSize: 16),
                            ),
                          ],
                        ),
                      )
                    : ListView.separated(
                        itemCount: _users.length,
                        separatorBuilder: (_, __) =>
                            const SizedBox(height: 12),
                        itemBuilder: (context, index) {
                          return _UserCard(
                            user: _users[index],
                            roleBadgeText: _roleBadgeText,
                            roleBadgeColor: _roleBadgeColor,
                            formatDate: _formatDate,
                            formatDateShort: _formatDateShort,
                          );
                        },
                      ),
          ),

          // ── Pagination ───────────────────────────────────────────
          Padding(
            padding: const EdgeInsets.only(top: 16),
            child: PaginationBar(
              currentPage: _currentPage,
              totalPages: _totalPages,
              onPageChanged: _goToPage,
            ),
          ),
        ],
      ),
    );
  }
}

// ─────────────────────────────────────────────────────────────────────────────
//  Stat Card
// ─────────────────────────────────────────────────────────────────────────────

class _StatCard extends StatelessWidget {
  final String label;
  final String value;
  final Color valueColor;

  const _StatCard({
    required this.label,
    required this.value,
    required this.valueColor,
  });

  @override
  Widget build(BuildContext context) {
    return Expanded(
      child: Container(
        padding: const EdgeInsets.all(20),
        decoration: BoxDecoration(
          color: Colors.white,
          border: Border.all(color: const Color(0xFFE5E7EB)),
          borderRadius: BorderRadius.circular(16),
          boxShadow: [
            BoxShadow(
              color: Colors.black.withValues(alpha: 0.03),
              blurRadius: 8,
              offset: const Offset(0, 2),
            ),
          ],
        ),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Text(
              label,
              style: const TextStyle(
                fontSize: 13,
                color: Color(0xFF6B7280),
              ),
            ),
            const SizedBox(height: 8),
            Text(
              value,
              style: TextStyle(
                fontSize: 28,
                fontWeight: FontWeight.w700,
                color: valueColor,
              ),
            ),
          ],
        ),
      ),
    );
  }
}

// ─────────────────────────────────────────────────────────────────────────────
//  User Card
// ─────────────────────────────────────────────────────────────────────────────

class _UserCard extends StatefulWidget {
  final AdminUserResponse user;
  final String Function(String) roleBadgeText;
  final Color Function(String) roleBadgeColor;
  final String Function(DateTime?) formatDate;
  final String Function(DateTime?) formatDateShort;

  const _UserCard({
    required this.user,
    required this.roleBadgeText,
    required this.roleBadgeColor,
    required this.formatDate,
    required this.formatDateShort,
  });

  @override
  State<_UserCard> createState() => _UserCardState();
}

class _UserCardState extends State<_UserCard> {
  bool _isHovering = false;

  @override
  Widget build(BuildContext context) {
    final u = widget.user;
    final roleColor = widget.roleBadgeColor(u.roleName);

    return MouseRegion(
      onEnter: (_) => setState(() => _isHovering = true),
      onExit: (_) => setState(() => _isHovering = false),
      child: AnimatedContainer(
        duration: const Duration(milliseconds: 200),
        padding: const EdgeInsets.all(20),
        decoration: BoxDecoration(
          color: Colors.white,
          borderRadius: BorderRadius.circular(16),
          border: Border.all(
            color: _isHovering
                ? const Color(0xFF0D7C66)
                : const Color(0xFFE5E7EB),
          ),
          boxShadow: _isHovering
              ? [
                  BoxShadow(
                    color: Colors.black.withValues(alpha: 0.08),
                    blurRadius: 16,
                    offset: const Offset(0, 4),
                  ),
                ]
              : [
                  BoxShadow(
                    color: Colors.black.withValues(alpha: 0.02),
                    blurRadius: 4,
                    offset: const Offset(0, 1),
                  ),
                ],
        ),
        child: Row(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            // ── Avatar ──
            Container(
              width: 56,
              height: 56,
              decoration: BoxDecoration(
                gradient: const LinearGradient(
                  begin: Alignment.topLeft,
                  end: Alignment.bottomRight,
                  colors: [Color(0xFF0D7C66), Color(0xFF0a6b57)],
                ),
                borderRadius: BorderRadius.circular(14),
              ),
              alignment: Alignment.center,
              child: Text(
                _initials(u.fullName),
                style: const TextStyle(
                  color: Colors.white,
                  fontSize: 20,
                  fontWeight: FontWeight.w700,
                ),
              ),
            ),

            const SizedBox(width: 20),

            // ── Info ──
            Expanded(
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  // Name row + badges
                  Row(
                    children: [
                      Expanded(
                        child: Column(
                          crossAxisAlignment: CrossAxisAlignment.start,
                          children: [
                            Text(
                              u.fullName,
                              style: const TextStyle(
                                fontSize: 18,
                                fontWeight: FontWeight.w700,
                                color: Color(0xFF111827),
                              ),
                            ),
                            const SizedBox(height: 6),
                            Wrap(
                              spacing: 8,
                              runSpacing: 6,
                              children: [
                                // Role badge
                                Container(
                                  padding: const EdgeInsets.symmetric(
                                      horizontal: 10, vertical: 4),
                                  decoration: BoxDecoration(
                                    color:
                                        roleColor.withValues(alpha: 0.1),
                                    borderRadius: BorderRadius.circular(20),
                                    border: Border.all(
                                        color: roleColor
                                            .withValues(alpha: 0.25)),
                                  ),
                                  child: Row(
                                    mainAxisSize: MainAxisSize.min,
                                    children: [
                                      Icon(LucideIcons.shield,
                                          size: 12, color: roleColor),
                                      const SizedBox(width: 4),
                                      Text(
                                        widget.roleBadgeText(u.roleName),
                                        style: TextStyle(
                                          fontSize: 12,
                                          fontWeight: FontWeight.w600,
                                          color: roleColor,
                                        ),
                                      ),
                                    ],
                                  ),
                                ),
                                // Status badge
                                Container(
                                  padding: const EdgeInsets.symmetric(
                                      horizontal: 10, vertical: 4),
                                  decoration: BoxDecoration(
                                    color: u.isActive
                                        ? const Color(0xFF22C55E)
                                            .withValues(alpha: 0.1)
                                        : const Color(0xFFEF4444)
                                            .withValues(alpha: 0.1),
                                    borderRadius: BorderRadius.circular(20),
                                  ),
                                  child: Text(
                                    u.isActive ? 'Aktivan' : 'Neaktivan',
                                    style: TextStyle(
                                      fontSize: 12,
                                      fontWeight: FontWeight.w600,
                                      color: u.isActive
                                          ? const Color(0xFF15803D)
                                          : const Color(0xFFDC2626),
                                    ),
                                  ),
                                ),
                              ],
                            ),
                          ],
                        ),
                      ),
                    ],
                  ),

                  const SizedBox(height: 16),

                  // ── Detail row ──
                  Row(
                    children: [
                      // Email
                      Expanded(
                        flex: 3,
                        child: Row(
                          children: [
                            const Icon(LucideIcons.mail,
                                size: 15, color: Color(0xFF9CA3AF)),
                            const SizedBox(width: 8),
                            Expanded(
                              child: Text(
                                u.email,
                                style: const TextStyle(
                                  fontSize: 13,
                                  color: Color(0xFF6B7280),
                                ),
                                maxLines: 1,
                                overflow: TextOverflow.ellipsis,
                              ),
                            ),
                          ],
                        ),
                      ),
                      // Phone
                      if (u.phoneNumber != null &&
                          u.phoneNumber!.isNotEmpty)
                        Expanded(
                          flex: 2,
                          child: Row(
                            children: [
                              const Icon(LucideIcons.phone,
                                  size: 15, color: Color(0xFF9CA3AF)),
                              const SizedBox(width: 8),
                              Expanded(
                                child: Text(
                                  u.phoneNumber!,
                                  style: const TextStyle(
                                    fontSize: 13,
                                    color: Color(0xFF6B7280),
                                  ),
                                  maxLines: 1,
                                  overflow: TextOverflow.ellipsis,
                                ),
                              ),
                            ],
                          ),
                        ),
                      // Created date
                      Expanded(
                        flex: 2,
                        child: RichText(
                          text: TextSpan(
                            style: const TextStyle(fontSize: 13),
                            children: [
                              const TextSpan(
                                text: 'Pridružen: ',
                                style: TextStyle(color: Color(0xFF9CA3AF)),
                              ),
                              TextSpan(
                                text: widget.formatDate(u.createdAt),
                                style: const TextStyle(
                                    color: Color(0xFF6B7280)),
                              ),
                            ],
                          ),
                        ),
                      ),
                      // Last login
                      Expanded(
                        flex: 2,
                        child: RichText(
                          text: TextSpan(
                            style: const TextStyle(fontSize: 13),
                            children: [
                              const TextSpan(
                                text: 'Posljednja Aktivnost: ',
                                style: TextStyle(color: Color(0xFF9CA3AF)),
                              ),
                              TextSpan(
                                text: widget.formatDateShort(u.lastLoginAt),
                                style: const TextStyle(
                                    color: Color(0xFF6B7280)),
                              ),
                            ],
                          ),
                        ),
                      ),
                    ],
                  ),

                  const SizedBox(height: 16),

                  // ── Bottom separator + email verified ──
                  Container(
                    padding: const EdgeInsets.only(top: 16),
                    decoration: const BoxDecoration(
                      border: Border(
                        top: BorderSide(color: Color(0xFFF3F4F6)),
                      ),
                    ),
                    child: Row(
                      children: [
                        // Email verification status
                        Row(
                          children: [
                            Icon(
                              u.isEmailVerified
                                  ? LucideIcons.badgeCheck
                                  : LucideIcons.badgeX,
                              size: 16,
                              color: u.isEmailVerified
                                  ? const Color(0xFF0D7C66)
                                  : const Color(0xFFEF4444),
                            ),
                            const SizedBox(width: 6),
                            Text(
                              u.isEmailVerified
                                  ? 'Email verifikovan'
                                  : 'Email nije verifikovan',
                              style: TextStyle(
                                fontSize: 12,
                                fontWeight: FontWeight.w500,
                                color: u.isEmailVerified
                                    ? const Color(0xFF0D7C66)
                                    : const Color(0xFFEF4444),
                              ),
                            ),
                          ],
                        ),

                        if (u.isFirstLogin) ...[
                          const SizedBox(width: 16),
                          Row(
                            children: [
                              const Icon(LucideIcons.logIn,
                                  size: 16, color: Color(0xFFF59E0B)),
                              const SizedBox(width: 6),
                              const Text(
                                'Prva prijava',
                                style: TextStyle(
                                  fontSize: 12,
                                  fontWeight: FontWeight.w500,
                                  color: Color(0xFFF59E0B),
                                ),
                              ),
                            ],
                          ),
                        ],

                        const Spacer(),

                        // Username chip
                        Container(
                          padding: const EdgeInsets.symmetric(
                              horizontal: 10, vertical: 4),
                          decoration: BoxDecoration(
                            color: const Color(0xFFF3F4F6),
                            borderRadius: BorderRadius.circular(8),
                          ),
                          child: Row(
                            mainAxisSize: MainAxisSize.min,
                            children: [
                              const Icon(LucideIcons.atSign,
                                  size: 12, color: Color(0xFF6B7280)),
                              const SizedBox(width: 4),
                              Text(
                                u.username,
                                style: const TextStyle(
                                  fontSize: 12,
                                  fontWeight: FontWeight.w500,
                                  color: Color(0xFF374151),
                                ),
                              ),
                            ],
                          ),
                        ),
                      ],
                    ),
                  ),
                ],
              ),
            ),
          ],
        ),
      ),
    );
  }

  String _initials(String name) {
    if (name.trim().isEmpty) return '?';
    return name
        .trim()
        .split(RegExp(r'\s+'))
        .take(2)
        .map((w) => w.isNotEmpty ? w[0].toUpperCase() : '')
        .join();
  }
}
