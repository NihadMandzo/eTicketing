import 'dart:async';

import 'package:flutter/material.dart';
import 'package:lucide_icons_flutter/lucide_icons.dart';
import '../models/responses/organization_response.dart';
import '../models/search_objects/base_search_object.dart';
import '../providers/organization_provider.dart';
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

  int _currentPage = 0;
  int _totalCount = 0;
  static const int _pageSize = 9;

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
      final result = await _provider.getAll(
        searchObject: searchObject,
        fromJson: OrganizationResponse.fromJson,
      );
      if (mounted) {
        setState(() {
          _organizations = result.items;
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
      builder: (ctx) => AlertDialog(
        shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(16)),
        title: const Text('Obriši organizaciju',
            style: TextStyle(fontWeight: FontWeight.w700)),
        content: Text(
            'Da li ste sigurni da želite obrisati organizaciju "${org.name}"?'),
        actions: [
          TextButton(
            onPressed: () => Navigator.of(ctx).pop(false),
            child: const Text('Odustani',
                style: TextStyle(color: Color(0xFF6B7280))),
          ),
          ElevatedButton(
            style: ElevatedButton.styleFrom(
              backgroundColor: const Color(0xFFEF4444),
              foregroundColor: Colors.white,
              shape: RoundedRectangleBorder(
                  borderRadius: BorderRadius.circular(10)),
            ),
            onPressed: () => Navigator.of(ctx).pop(true),
            child: const Text('Obriši'),
          ),
        ],
      ),
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
        ScaffoldMessenger.of(context).showSnackBar(
          SnackBar(
            content: Row(
              children: [
                const Icon(Icons.check_circle_outline, color: Colors.white),
                const SizedBox(width: 10),
                Text('Organizacija "${org.name}" je uspješno obrisana'),
              ],
            ),
            backgroundColor: const Color(0xFF0D7C66),
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

  @override
  void dispose() {
    _debounce?.cancel();
    _searchController.dispose();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    return Padding(
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
                  children: const [
                    Text(
                      'Organizacije',
                      style: TextStyle(
                        fontSize: 30,
                        fontWeight: FontWeight.w700,
                        color: Color(0xFF111827),
                        height: 1.2,
                      ),
                    ),
                    SizedBox(height: 8),
                    Text(
                      'Upravljajte svim organizacijama na platformi',
                      style:
                          TextStyle(fontSize: 16, color: Color(0xFF4B5563)),
                    ),
                  ],
                ),
              ),
              const SizedBox(width: 16),
              Container(
                decoration: BoxDecoration(
                  gradient: const LinearGradient(
                    colors: [Color(0xFF0D7C66), Color(0xFF0a6b57)],
                  ),
                  borderRadius: BorderRadius.circular(12),
                  boxShadow: [
                    BoxShadow(
                      color: const Color(0xFF0D7C66).withValues(alpha: 0.3),
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
                        children: const [
                          Icon(LucideIcons.plus,
                              color: Colors.white, size: 20),
                          SizedBox(width: 8),
                          Text(
                            'Dodaj Organizaciju',
                            style: TextStyle(
                              color: Colors.white,
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

          // ── Search ────────────────────────────────────────────────
          Container(
            height: 44,
            decoration: BoxDecoration(
              color: Colors.white,
              border: Border.all(color: const Color(0xFFD1D5DB)),
              borderRadius: BorderRadius.circular(12),
            ),
            child: TextField(
              controller: _searchController,
              onChanged: _onSearchChanged,
              decoration: const InputDecoration(
                hintText: 'Pretražite organizacije...',
                hintStyle: TextStyle(color: Color(0xFF6B7280)),
                prefixIcon: Icon(LucideIcons.search,
                    color: Color(0xFF9CA3AF), size: 18),
                border: InputBorder.none,
                contentPadding: EdgeInsets.symmetric(vertical: 12),
              ),
              style: const TextStyle(color: Color(0xFF111827)),
            ),
          ),

          const SizedBox(height: 12),

          // ── Count ─────────────────────────────────────────────────
          if (!_isLoading)
            Padding(
              padding: const EdgeInsets.only(bottom: 8),
              child: Text(
                '$_totalCount organizacija',
                style: const TextStyle(
                    fontSize: 13, color: Color(0xFF6B7280)),
              ),
            ),

          // ── Grid ──────────────────────────────────────────────────
          Expanded(
            child: _isLoading
                ? const Center(
                    child: CircularProgressIndicator(
                        color: Color(0xFF0D7C66)),
                  )
                : _organizations.isEmpty
                    ? Center(
                        child: Column(
                          mainAxisSize: MainAxisSize.min,
                          children: const [
                            Icon(LucideIcons.building2,
                                size: 48, color: Color(0xFFD1D5DB)),
                            SizedBox(height: 12),
                            Text(
                              'Nema organizacija',
                              style: TextStyle(
                                  color: Color(0xFF6B7280), fontSize: 16),
                            ),
                          ],
                        ),
                      )
                    : LayoutBuilder(
                        builder: (context, constraints) {
                          int crossAxisCount = 3;
                          if (constraints.maxWidth >= 1400) {
                            crossAxisCount = 5;
                          } else if (constraints.maxWidth >= 1100) {
                            crossAxisCount = 4;
                          } else if (constraints.maxWidth >= 800) {
                            crossAxisCount = 3;
                          }

                          return GridView.builder(
                            gridDelegate:
                                SliverGridDelegateWithFixedCrossAxisCount(
                              crossAxisCount: crossAxisCount,
                              crossAxisSpacing: 14,
                              mainAxisSpacing: 14,
                              childAspectRatio: 1.0,
                            ),
                            itemCount: _organizations.length,
                            itemBuilder: (context, index) {
                              final org = _organizations[index];
                              return _OrganizationCard(
                                organization: org,
                                onEdit: () =>
                                    _openDialog(organization: org),
                                onDelete: () =>
                                    _deleteOrganization(org),
                              );
                            },
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

// ─────────────────────────────────────────────────────────────
//  Organization Card
// ─────────────────────────────────────────────────────────────
class _OrganizationCard extends StatefulWidget {
  final OrganizationResponse organization;
  final VoidCallback onEdit;
  final VoidCallback onDelete;

  const _OrganizationCard({
    required this.organization,
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

    return MouseRegion(
      onEnter: (_) => setState(() => _isHovering = true),
      onExit: (_) => setState(() => _isHovering = false),
      cursor: SystemMouseCursors.click,
      child: AnimatedContainer(
        duration: const Duration(milliseconds: 200),
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
              // ── Logo ──
              Center(
                child: ClipRRect(
                  borderRadius: BorderRadius.circular(14),
                  child: (org.logoUrl != null && org.logoUrl!.isNotEmpty)
                      ? Image.network(
                          org.logoUrl!,
                          width: 100,
                          height: 100,
                          fit: BoxFit.cover,
                          errorBuilder: (context, error, stackTrace) =>
                              _LogoFallback(name: org.name, size: 100),
                        )
                      : _LogoFallback(name: org.name, size: 100),
                ),
              ),

              const SizedBox(height: 16),

              // ── Name ──
              Text(
                org.name,
                style: const TextStyle(
                  fontSize: 16,
                  fontWeight: FontWeight.w700,
                  color: Color(0xFF111827),
                ),
                maxLines: 1,
                overflow: TextOverflow.ellipsis,
                textAlign: TextAlign.center,
              ),

              const SizedBox(height: 12),

              // ── Contact info ──
              Expanded(
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    _ContactRow(emoji: '📧', text: org.email),
                    const SizedBox(height: 6),
                    _ContactRow(emoji: '📞', text: org.phoneNumber),
                    const SizedBox(height: 6),
                    _ContactRow(emoji: '📍', text: org.address),

                    const Spacer(),

                    // ── User count chip ──
                    Container(
                      padding: const EdgeInsets.symmetric(
                          horizontal: 10, vertical: 5),
                      decoration: BoxDecoration(
                        color: const Color(0xFF0D7C66).withValues(alpha: 0.08),
                        borderRadius: BorderRadius.circular(8),
                      ),
                      child: Row(
                        mainAxisSize: MainAxisSize.min,
                        children: [
                          const Icon(LucideIcons.users,
                              size: 14, color: Color(0xFF0D7C66)),
                          const SizedBox(width: 6),
                          Text(
                            '${org.userCount} korisnika',
                            style: const TextStyle(
                              fontSize: 13,
                              fontWeight: FontWeight.w600,
                              color: Color(0xFF0D7C66),
                            ),
                          ),
                        ],
                      ),
                    ),
                  ],
                ),
              ),

              const SizedBox(height: 14),

              // ── Action buttons ──
              Row(
                children: [
                  Expanded(
                    child: _ActionButton(
                      icon: LucideIcons.pencil,
                      label: 'Pregled i izmjene',
                      isPrimary: true,
                      onTap: widget.onEdit,
                    ),
                  ),
                  const SizedBox(width: 8),
                  _ActionButton(
                    icon: LucideIcons.trash2,
                    label: 'Obriši',
                    isPrimary: false,
                    isDestructive: true,
                    onTap: widget.onDelete,
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
    return Row(
      children: [
        Text(emoji, style: const TextStyle(fontSize: 13)),
        const SizedBox(width: 8),
        Expanded(
          child: Text(
            text,
            style:
                const TextStyle(fontSize: 13, color: Color(0xFF4B5563)),
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
    final Color fg;
    final Color bg;
    if (isDestructive) {
      fg = const Color(0xFFEF4444);
      bg = const Color(0xFFEF4444).withValues(alpha: 0.08);
    } else if (isPrimary) {
      fg = const Color(0xFF0D7C66);
      bg = const Color(0xFF0D7C66).withValues(alpha: 0.1);
    } else {
      fg = const Color(0xFF374151);
      bg = const Color(0xFFF3F4F6);
    }

    return Material(
      color: bg,
      borderRadius: BorderRadius.circular(12),
      child: InkWell(
        borderRadius: BorderRadius.circular(12),
        onTap: onTap,
        child: Container(
          padding: const EdgeInsets.symmetric(vertical: 9, horizontal: 14),
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
              Text(
                label,
                style: TextStyle(
                  fontSize: 13,
                  fontWeight: FontWeight.w600,
                  color: fg,
                ),
              ),
            ],
          ),
        ),
      ),
    );
  }
}

// ─── Logo fallback ─────────────────────────────────────────────────────────────────────────────────

class _LogoFallback extends StatelessWidget {
  final String name;
  final double size;

  const _LogoFallback({required this.name, required this.size});

  @override
  Widget build(BuildContext context) {
    final initials = name.isNotEmpty
        ? name.trim().split(RegExp(r'\s+')).take(2).map((w) => w[0].toUpperCase()).join()
        : '?';
    return Container(
      width: size,
      height: size,
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
        initials,
        style: TextStyle(
          color: Colors.white,
          fontSize: size * 0.34,
          fontWeight: FontWeight.w700,
          letterSpacing: 1,
        ),
      ),
    );
  }
}
