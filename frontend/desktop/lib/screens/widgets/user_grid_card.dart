import 'package:flutter/material.dart';
import 'package:lucide_icons_flutter/lucide_icons.dart';

import '../../models/responses/admin_user_response.dart';
import '../../theme/app_colors.dart';
import '../../theme/role_badge.dart';

/// Compact card-grid tile for a staff/org user — shared by the SuperAdmin
/// platform Users screen, the OrganizationSuperAdmin self-service Users tab,
/// and the organization detail screen's consolidated Users tab. Mirrors the
/// visual language of `_OrganizationCard`/`_CategoryCard` (avatar, badges,
/// action buttons) so all card-grid screens in the app look like one system.
class UserGridCard extends StatefulWidget {
  final AdminUserResponse user;
  final VoidCallback? onEdit;
  final VoidCallback? onDelete;
  final VoidCallback? onSetPassword;

  const UserGridCard({
    super.key,
    required this.user,
    this.onEdit,
    this.onDelete,
    this.onSetPassword,
  });

  @override
  State<UserGridCard> createState() => _UserGridCardState();
}

class _UserGridCardState extends State<UserGridCard> {
  bool _isHovering = false;

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
    final u = widget.user;
    final isDark = Theme.of(context).brightness == Brightness.dark;
    final primaryColor = isDark ? AppColors.secondary : AppColors.primary;
    final textPrimary = isDark ? AppColors.darkTextPrimary : AppColors.lightTextPrimary;
    final textTertiary = isDark ? AppColors.darkTextTertiary : AppColors.lightTextTertiary;
    final roleColor = roleBadgeColor(u.roleName, isDark);
    final hasActions = widget.onEdit != null || widget.onDelete != null;

    return MouseRegion(
      onEnter: (_) => setState(() => _isHovering = true),
      onExit: (_) => setState(() => _isHovering = false),
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
          padding: const EdgeInsets.all(12),
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              // ── Avatar + name ── smaller avatar (48, was 64) and tighter gaps
              // than other card-grid screens — this card is deliberately more
              // compact than Category/Organization/Product cards, same info.
              Center(
                child: Column(
                  children: [
                    Container(
                      width: 48,
                      height: 48,
                      decoration: BoxDecoration(
                        gradient: LinearGradient(
                          begin: Alignment.topLeft,
                          end: Alignment.bottomRight,
                          colors: isDark
                              ? [AppColors.secondary, AppColors.primary]
                              : [AppColors.primary, AppColors.primaryDark],
                        ),
                        borderRadius: BorderRadius.circular(14),
                      ),
                      alignment: Alignment.center,
                      child: Text(
                        _initials(u.fullName),
                        style: TextStyle(
                          color: isDark ? AppColors.darkBackground : Colors.white,
                          fontSize: 16,
                          fontWeight: FontWeight.w700,
                        ),
                      ),
                    ),
                    const SizedBox(height: 6),
                    Text(
                      u.fullName,
                      style: TextStyle(fontSize: 14, fontWeight: FontWeight.w700, color: textPrimary),
                      maxLines: 1,
                      overflow: TextOverflow.ellipsis,
                      textAlign: TextAlign.center,
                    ),
                  ],
                ),
              ),

              const SizedBox(height: 6),

              // ── Badges ──
              Center(
                child: Wrap(
                  alignment: WrapAlignment.center,
                  spacing: 5,
                  runSpacing: 5,
                  children: [
                    _Badge(
                      icon: LucideIcons.shield,
                      text: roleBadgeText(u.roleName),
                      color: roleColor,
                    ),
                    _Badge(
                      text: u.isActive ? 'Aktivan' : 'Neaktivan',
                      color: u.isActive ? AppColors.successDark : AppColors.errorDark,
                    ),
                    if (u.organizationName != null)
                      _Badge(
                        icon: LucideIcons.building2,
                        text: u.organizationName!,
                        color: isDark ? AppColors.darkTextSecondary : AppColors.lightTextSecondary,
                        // The one free-text badge here, and the only one that can grow without
                        // bound — capped so it takes at most one Wrap run of its own.
                        maxWidth: 150,
                      ),
                  ],
                ),
              ),

              const SizedBox(height: 8),

              // ── Contact info ── plain (not Expanded/Flexible) — the card now
              // has a fixed pixel height (see the grid's mainAxisExtent), not an
              // elastic aspect-ratio height. Wrapping this in Flexible previously
              // clamped it to whatever leftover height the Column's flex
              // algorithm computed, which could be smaller than its natural
              // content height, overflowing regardless of the Flexible. Laying
              // it out top-down at natural size and giving the card generous
              // fixed height headroom is what actually guarantees no overflow.
              Row(
                children: [
                  Icon(LucideIcons.mail, size: 12, color: textTertiary),
                  const SizedBox(width: 5),
                  Expanded(
                    child: Text(
                      u.email,
                      style: TextStyle(fontSize: 11, color: textTertiary),
                      maxLines: 1,
                      overflow: TextOverflow.ellipsis,
                    ),
                  ),
                ],
              ),
              if (u.phoneNumber != null && u.phoneNumber!.isNotEmpty) ...[
                const SizedBox(height: 3),
                Row(
                  children: [
                    Icon(LucideIcons.phone, size: 12, color: textTertiary),
                    const SizedBox(width: 5),
                    Expanded(
                      child: Text(
                        u.phoneNumber!,
                        style: TextStyle(fontSize: 11, color: textTertiary),
                        maxLines: 1,
                        overflow: TextOverflow.ellipsis,
                      ),
                    ),
                  ],
                ),
              ],
              const SizedBox(height: 3),
              Row(
                children: [
                  Icon(LucideIcons.atSign, size: 12, color: textTertiary),
                  const SizedBox(width: 5),
                  Expanded(
                    child: Text(
                      u.username,
                      style: TextStyle(fontSize: 11, color: textTertiary),
                      maxLines: 1,
                      overflow: TextOverflow.ellipsis,
                    ),
                  ),
                ],
              ),

              // Spacer (not Expanded around fixed content) — safe by
              // construction, its minimum size is 0, so it can never be the
              // cause of an overflow; it only pushes the buttons down when
              // there's genuine leftover room.
              const Spacer(),

              // ── Action buttons ──
              if (hasActions) ...[
                const SizedBox(height: 8),
                Row(
                  children: [
                    if (widget.onEdit != null)
                      Expanded(
                        child: _CardActionButton(
                          icon: LucideIcons.pencil,
                          label: 'Uredi',
                          isDestructive: false,
                          onTap: widget.onEdit!,
                        ),
                      ),
                    if (widget.onEdit != null && widget.onDelete != null) const SizedBox(width: 6),
                    if (widget.onDelete != null)
                      Expanded(
                        child: _CardActionButton(
                          icon: LucideIcons.trash2,
                          label: 'Obriši',
                          isDestructive: true,
                          onTap: widget.onDelete!,
                        ),
                      ),
                  ],
                ),
              ],
              // Stacked below Edit/Delete, not crammed into the same row —
              // three narrow buttons wouldn't fit this card's width cleanly.
              if (widget.onSetPassword != null) ...[
                const SizedBox(height: 6),
                _CardActionButton(
                  icon: LucideIcons.keyRound,
                  label: 'Promijeni Lozinku',
                  isDestructive: false,
                  onTap: widget.onSetPassword!,
                ),
              ],
            ],
          ),
        ),
      ),
    );
  }
}

class _Badge extends StatelessWidget {
  final IconData? icon;
  final String text;
  final Color color;

  /// Caps how wide this badge may grow. Free-text badges (an organization name) pass one —
  /// without it a long name makes the badge as wide as its text, pushing the enclosing Wrap onto
  /// an extra run and overflowing the card's fixed height. Fixed-vocabulary badges (role, active
  /// state) leave it null: their text is short and known, and clipping them would lose meaning.
  final double? maxWidth;

  const _Badge({this.icon, required this.text, required this.color, this.maxWidth});

  @override
  Widget build(BuildContext context) {
    final isDark = Theme.of(context).brightness == Brightness.dark;
    return ConstrainedBox(
      constraints: BoxConstraints(maxWidth: maxWidth ?? double.infinity),
      child: Container(
        padding: const EdgeInsets.symmetric(horizontal: 8, vertical: 3),
        decoration: BoxDecoration(
          color: color.withValues(alpha: isDark ? 0.18 : 0.1),
          borderRadius: BorderRadius.circular(20),
        ),
        child: Row(
          mainAxisSize: MainAxisSize.min,
          children: [
            if (icon != null) ...[
              Icon(icon, size: 11, color: color),
              const SizedBox(width: 3),
            ],
            // Flexible, not a bare Text — ellipsis only engages once the Text has a bounded
            // width to be measured against, which maxWidth above is what supplies.
            Flexible(
              child: Text(
                text,
                style: TextStyle(fontSize: 11, fontWeight: FontWeight.w600, color: color),
                maxLines: 1,
                overflow: TextOverflow.ellipsis,
              ),
            ),
          ],
        ),
      ),
    );
  }
}

class _CardActionButton extends StatelessWidget {
  final IconData icon;
  final String label;
  final bool isDestructive;
  final VoidCallback onTap;

  const _CardActionButton({
    required this.icon,
    required this.label,
    required this.isDestructive,
    required this.onTap,
  });

  @override
  Widget build(BuildContext context) {
    final isDark = Theme.of(context).brightness == Brightness.dark;
    final primaryColor = isDark ? AppColors.secondary : AppColors.primary;
    final fg = isDestructive ? AppColors.error : primaryColor;
    final bg = isDestructive
        ? AppColors.error.withValues(alpha: isDark ? 0.16 : 0.08)
        : primaryColor.withValues(alpha: isDark ? 0.18 : 0.1);

    return Material(
      color: bg,
      borderRadius: BorderRadius.circular(10),
      child: InkWell(
        borderRadius: BorderRadius.circular(10),
        onTap: onTap,
        child: Container(
          padding: const EdgeInsets.symmetric(vertical: 8),
          decoration: BoxDecoration(
            borderRadius: BorderRadius.circular(10),
            border: Border.all(color: fg.withValues(alpha: 0.2)),
          ),
          child: Row(
            mainAxisAlignment: MainAxisAlignment.center,
            children: [
              Icon(icon, size: 14, color: fg),
              const SizedBox(width: 5),
              // Flexible+ellipsis (not a bare Text) — this button sits inside an
              // Expanded whose actual width depends on the grid's computed
              // column count, which can be narrower than the card's nominal
              // maxCrossAxisExtent. Without this, the label can cause a
              // RenderFlex right-overflow instead of just truncating.
              Flexible(
                child: Text(
                  label,
                  style: TextStyle(fontSize: 12, fontWeight: FontWeight.w600, color: fg),
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
