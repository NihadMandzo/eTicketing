import 'package:flutter/material.dart';

import '../../models/responses/user_profile.dart';
import '../../theme/app_colors.dart';
import '../../theme/theme_controller.dart';
import '../../core/export_notifications.dart';
import '../../models/responses/ticket_print_batch_response.dart';
import '../../providers/ticket_print_provider.dart';
import '../../main.dart';
import 'ticket_export_download.dart';

class AppHeader extends StatefulWidget {
  final UserProfile user;
  final VoidCallback onLogout;

  const AppHeader({
    super.key,
    required this.user,
    required this.onLogout,
  });

  @override
  State<AppHeader> createState() => _AppHeaderState();
}

class _AppHeaderState extends State<AppHeader> {
  bool _menuOpen = false;
  final _layerLink = LayerLink();
  OverlayEntry? _overlay;

  String _roleDisplay(String role) {
    switch (role) {
      case 'SuperAdmin':
        return 'Super Administrator';
      case 'Admin':
        return 'Administrator';
      case 'OrganizationSuperAdmin':
        return 'Org. Super Administrator';
      case 'OrganizationAdmin':
        return 'Org. Administrator';
      default:
        return role;
    }
  }

  // ── Overlay lifecycle ─────────────────────────────────────────────────────

  void _openMenu() {
    if (_menuOpen) return;
    setState(() => _menuOpen = true);
    _overlay = OverlayEntry(builder: (_) => _buildOverlay());
    // rootOverlay: true — defensive: always anchor to the app's outermost Overlay rather than
    // the nearest one, so this keeps working even if a future caller ever renders AppHeader
    // inside a dialog/route that provides its own nested Overlay.
    Overlay.of(context, rootOverlay: true).insert(_overlay!);
  }

  void _closeMenu() {
    if (!_menuOpen) return;
    setState(() => _menuOpen = false);
    _overlay?.remove();
    _overlay = null;
  }

  void _toggle() => _menuOpen ? _closeMenu() : _openMenu();

  @override
  void dispose() {
    _overlay?.remove();
    super.dispose();
  }

  // ── Overlay widget ────────────────────────────────────────────────────────

  Widget _buildOverlay() {
    return Stack(
      children: [
        Positioned.fill(
          child: GestureDetector(
            behavior: HitTestBehavior.translucent,
            onTap: _closeMenu,
          ),
        ),
        CompositedTransformFollower(
          link: _layerLink,
          showWhenUnlinked: false,
          targetAnchor: Alignment.bottomRight,
          followerAnchor: Alignment.topRight,
          offset: const Offset(0, 8),
          child: Material(
            color: Colors.transparent,
            child: _DropdownCard(
              user: widget.user,
              roleDisplay: _roleDisplay(widget.user.roleName),
              onLogout: () {
                _closeMenu();
                widget.onLogout();
              },
            ),
          ),
        ),
      ],
    );
  }

  // ── Avatar ────────────────────────────────────────────────────────────────

  Widget _avatar({double size = 36}) {
    final f = widget.user.firstName.isNotEmpty
        ? widget.user.firstName[0].toUpperCase()
        : '';
    final l = widget.user.lastName.isNotEmpty
        ? widget.user.lastName[0].toUpperCase()
        : '';
    final initials = f.isEmpty && l.isEmpty ? '?' : '$f$l';
    final isDark = Theme.of(context).brightness == Brightness.dark;
    return Container(
      width: size,
      height: size,
      decoration: BoxDecoration(
        shape: BoxShape.circle,
        gradient: LinearGradient(
          begin: Alignment.topLeft,
          end: Alignment.bottomRight,
          colors: isDark
              ? [AppColors.secondary, AppColors.primary]
              : [AppColors.primary, AppColors.primaryDark],
        ),
      ),
      alignment: Alignment.center,
      child: Text(
        initials,
        style: TextStyle(
          color: isDark ? AppColors.darkBackground : Colors.white,
          fontWeight: FontWeight.w700,
          fontSize: size * 0.36,
        ),
      ),
    );
  }

  @override
  Widget build(BuildContext context) {
    final isDark = Theme.of(context).brightness == Brightness.dark;
    return Container(
      height: 64,
      decoration: BoxDecoration(
        color: isDark ? AppColors.darkSurface : Colors.white,
        border: Border(
          bottom: BorderSide(color: isDark ? AppColors.darkBorder : AppColors.lightBorder),
        ),
        boxShadow: isDark
            ? null
            : const [
                BoxShadow(
                  color: Color(0x08000000),
                  blurRadius: 8,
                  offset: Offset(0, 2),
                ),
              ],
      ),
      padding: const EdgeInsets.symmetric(horizontal: 24),
      child: Row(
        children: [
          // ── Search spacer ──────────────────────────────────────────────────────
          const Spacer(),

          const SizedBox(width: 16),

          // ── Notification bell ────────────────────────────────────────────
          const _BellButton(),

          const SizedBox(width: 8),

          // ── User button ──────────────────────────────────────────────────
          CompositedTransformTarget(
            link: _layerLink,
            child: _UserButton(
              avatar: _avatar(),
              name: widget.user.fullName,
              role: _roleDisplay(widget.user.roleName),
              isOpen: _menuOpen,
              onTap: _toggle,
            ),
          ),
        ],
      ),
    );
  }
}

// ─── User trigger button ──────────────────────────────────────────────────────

class _UserButton extends StatefulWidget {
  final Widget avatar;
  final String name;
  final String role;
  final bool isOpen;
  final VoidCallback onTap;

  const _UserButton({
    required this.avatar,
    required this.name,
    required this.role,
    required this.isOpen,
    required this.onTap,
  });

  @override
  State<_UserButton> createState() => _UserButtonState();
}

class _UserButtonState extends State<_UserButton> {
  bool _hovered = false;

  @override
  Widget build(BuildContext context) {
    final isDark = Theme.of(context).brightness == Brightness.dark;
    return MouseRegion(
      cursor: SystemMouseCursors.click,
      onEnter: (_) => setState(() => _hovered = true),
      onExit: (_) => setState(() => _hovered = false),
      child: GestureDetector(
        onTap: widget.onTap,
        child: AnimatedContainer(
          duration: const Duration(milliseconds: 150),
          padding:
              const EdgeInsets.symmetric(horizontal: 10, vertical: 6),
          decoration: BoxDecoration(
            color: _hovered || widget.isOpen
                ? (isDark ? AppColors.darkSurfaceMuted : AppColors.lightSurfaceMuted)
                : Colors.transparent,
            borderRadius: BorderRadius.circular(10),
          ),
          child: Row(
            mainAxisSize: MainAxisSize.min,
            children: [
              widget.avatar,
              const SizedBox(width: 10),
              Column(
                mainAxisAlignment: MainAxisAlignment.center,
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  Text(
                    widget.name,
                    style: TextStyle(
                      fontSize: 13,
                      fontWeight: FontWeight.w600,
                      color: isDark ? AppColors.darkTextPrimary : AppColors.lightTextPrimary,
                    ),
                  ),
                  Text(
                    widget.role,
                    style: TextStyle(
                      fontSize: 11,
                      color: isDark ? AppColors.darkTextTertiary : AppColors.lightTextTertiary,
                    ),
                  ),
                ],
              ),
              const SizedBox(width: 6),
              AnimatedRotation(
                turns: widget.isOpen ? 0.5 : 0,
                duration: const Duration(milliseconds: 200),
                child: Icon(
                  Icons.keyboard_arrow_down_rounded,
                  size: 18,
                  color: isDark ? AppColors.darkTextTertiary : AppColors.lightTextTertiary,
                ),
              ),
            ],
          ),
        ),
      ),
    );
  }
}

// ─── Dropdown card (matches the React design) ─────────────────────────────────

class _DropdownCard extends StatelessWidget {
  final UserProfile user;
  final String roleDisplay;
  final VoidCallback onLogout;

  const _DropdownCard({
    required this.user,
    required this.roleDisplay,
    required this.onLogout,
  });

  @override
  Widget build(BuildContext context) {
    final isDark = Theme.of(context).brightness == Brightness.dark;
    final borderColor = isDark ? AppColors.darkBorder : AppColors.lightBorder;
    return Container(
      width: 264,
      decoration: BoxDecoration(
        color: isDark ? AppColors.darkSurface : Colors.white,
        borderRadius: BorderRadius.circular(14),
        border: Border.all(color: borderColor),
        boxShadow: [
          BoxShadow(
            color: Colors.black.withValues(alpha: isDark ? 0.4 : 0.12),
            blurRadius: 28,
            offset: const Offset(0, 8),
          ),
        ],
      ),
      child: Column(
        mainAxisSize: MainAxisSize.min,
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          // User info section
          Padding(
            padding: const EdgeInsets.fromLTRB(16, 16, 16, 14),
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Text(
                  user.fullName,
                  style: TextStyle(
                    fontSize: 15,
                    fontWeight: FontWeight.w600,
                    color: isDark ? AppColors.darkTextPrimary : AppColors.lightTextPrimary,
                  ),
                ),
                const SizedBox(height: 3),
                Text(
                  user.email,
                  style: TextStyle(
                    fontSize: 13,
                    color: isDark ? AppColors.darkTextTertiary : AppColors.lightTextTertiary,
                  ),
                ),
                if (user.organizationName != null) ...[
                  const SizedBox(height: 4),
                  Text(
                    user.organizationName!,
                    style: TextStyle(
                      fontSize: 13,
                      fontWeight: FontWeight.w500,
                      color: isDark ? AppColors.secondary : AppColors.primary,
                    ),
                  ),
                ],
              ],
            ),
          ),

          Divider(height: 1, thickness: 1, color: borderColor),

          // Dark/light mode — the only place this toggle lives now (moved out of the
          // Postavke dialog's removed "preferences" tab, see settings_dialog.dart).
          const Padding(
            padding: EdgeInsets.fromLTRB(8, 4, 8, 0),
            child: _ThemeToggleRow(),
          ),

          Divider(height: 1, thickness: 1, color: borderColor),

          // Logout
          _DropdownAction(
            icon: Icons.logout_rounded,
            label: 'Odjavi se',
            color: isDark ? AppColors.error : AppColors.error,
            hoverBg: isDark ? AppColors.errorBgDarkMode : AppColors.errorBg,
            onTap: onLogout,
          ),

          const SizedBox(height: 4),
        ],
      ),
    );
  }
}

// ─── Dark/light mode toggle row ────────────────────────────────────────────────

class _ThemeToggleRow extends StatelessWidget {
  const _ThemeToggleRow();

  @override
  Widget build(BuildContext context) {
    return ValueListenableBuilder<ThemeMode>(
      valueListenable: ThemeController.mode,
      builder: (context, mode, _) {
        final isDark = ThemeController.isDark(context);
        final primary = isDark ? AppColors.secondary : AppColors.primary;
        final textPrimary = isDark ? AppColors.darkTextPrimary : AppColors.lightTextPrimary;
        return Padding(
          padding: const EdgeInsets.symmetric(horizontal: 4, vertical: 4),
          child: Row(
            children: [
              Icon(
                isDark ? Icons.dark_mode_outlined : Icons.light_mode_outlined,
                size: 18,
                color: primary,
              ),
              const SizedBox(width: 10),
              Expanded(
                child: Text(
                  'Tamni Način Rada',
                  style: TextStyle(fontSize: 14, fontWeight: FontWeight.w500, color: textPrimary),
                ),
              ),
              Switch(
                value: isDark,
                activeThumbColor: primary,
                onChanged: (v) => ThemeController.setMode(v ? ThemeMode.dark : ThemeMode.light),
              ),
            ],
          ),
        );
      },
    );
  }
}

// ─── Dropdown action row ──────────────────────────────────────────────────────

class _DropdownAction extends StatefulWidget {
  final IconData icon;
  final String label;
  final Color color;
  final Color hoverBg;
  final VoidCallback onTap;

  const _DropdownAction({
    required this.icon,
    required this.label,
    required this.color,
    required this.hoverBg,
    required this.onTap,
  });

  @override
  State<_DropdownAction> createState() => _DropdownActionState();
}

class _DropdownActionState extends State<_DropdownAction> {
  bool _hovered = false;

  @override
  Widget build(BuildContext context) {
    return MouseRegion(
      cursor: SystemMouseCursors.click,
      onEnter: (_) => setState(() => _hovered = true),
      onExit: (_) => setState(() => _hovered = false),
      child: GestureDetector(
        onTap: widget.onTap,
        child: Padding(
          padding: const EdgeInsets.fromLTRB(8, 4, 8, 0),
          child: AnimatedContainer(
            duration: const Duration(milliseconds: 120),
            decoration: BoxDecoration(
              color: _hovered ? widget.hoverBg : Colors.transparent,
              borderRadius: BorderRadius.circular(8),
            ),
            padding:
                const EdgeInsets.symmetric(horizontal: 12, vertical: 11),
            child: Row(
              children: [
                Icon(widget.icon, size: 18, color: widget.color),
                const SizedBox(width: 10),
                Text(
                  widget.label,
                  style: TextStyle(
                    fontSize: 14,
                    fontWeight: FontWeight.w500,
                    color: widget.color,
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

// ─── Notification bell ────────────────────────────────────────────────────────

/// Tells the organizer when a ticket export they started elsewhere in the app
/// has finished rendering, and lets them collect it without navigating back to
/// the product it belongs to.
///
/// The dot is not decorative: it appears only when there is a PDF waiting or a
/// render that failed. With nothing outstanding the bell stays quiet, so a lit
/// dot always means there is something to act on.
class _BellButton extends StatefulWidget {
  const _BellButton();

  @override
  State<_BellButton> createState() => _BellButtonState();
}

class _BellButtonState extends State<_BellButton> {
  bool _hovered = false;

  /// Always opens, even with nothing outstanding — the panel renders its own
  /// empty state, which is friendlier than a bell that ignores the click.
  Future<void> _openPanel() =>
      showDialog<void>(context: context, builder: (_) => const _ExportsDialog());

  @override
  Widget build(BuildContext context) {
    final isDark = Theme.of(context).brightness == Brightness.dark;

    return ValueListenableBuilder<List<TicketPrintBatchResponse>>(
      valueListenable: exportNotifications.batches,
      builder: (context, batches, _) {
        final hasReady = batches.any((b) => b.isDownloadable);
        final hasFailed = batches.any((b) => b.status == TicketPrintBatchStatus.failed);
        final showDot = hasReady || hasFailed;

        return Tooltip(
          message: hasReady
              ? 'PDF sa ulaznicama je spreman za preuzimanje'
              : hasFailed
                  ? 'Izvoz ulaznica nije uspio'
                  : 'Obavještenja',
          child: MouseRegion(
            cursor: SystemMouseCursors.click,
            onEnter: (_) => setState(() => _hovered = true),
            onExit: (_) => setState(() => _hovered = false),
            child: GestureDetector(
              onTap: _openPanel,
              child: AnimatedContainer(
                duration: const Duration(milliseconds: 150),
                width: 40,
                height: 40,
                decoration: BoxDecoration(
                  color: _hovered
                      ? (isDark ? AppColors.darkSurfaceMuted : AppColors.lightSurfaceMuted)
                      : Colors.transparent,
                  borderRadius: BorderRadius.circular(10),
                ),
                child: Stack(
                  children: [
                    Center(
                      child: Icon(
                        Icons.notifications_outlined,
                        size: 22,
                        color: _hovered
                            ? (isDark ? AppColors.darkTextPrimary : AppColors.lightTextPrimary)
                            : (isDark ? AppColors.darkTextTertiary : AppColors.lightTextTertiary),
                      ),
                    ),
                    if (showDot)
                      Positioned(
                        top: 8,
                        right: 8,
                        child: Container(
                          width: 8,
                          height: 8,
                          decoration: BoxDecoration(
                            color: hasFailed ? AppColors.error : AppColors.accent,
                            shape: BoxShape.circle,
                          ),
                        ),
                      ),
                  ],
                ),
              ),
            ),
          ),
        );
      },
    );
  }
}

/// The list behind the bell — one row per outstanding ticket export.
class _ExportsDialog extends StatelessWidget {
  const _ExportsDialog();

  @override
  Widget build(BuildContext context) {
    final isDark = Theme.of(context).brightness == Brightness.dark;
    final surface = isDark ? AppColors.darkSurface : Colors.white;
    final border = isDark ? AppColors.darkBorder : AppColors.lightBorder;

    return Dialog(
      backgroundColor: Colors.transparent,
      insetPadding: const EdgeInsets.all(24),
      child: ConstrainedBox(
        constraints: const BoxConstraints(maxWidth: 480),
        child: ClipRRect(
          borderRadius: BorderRadius.circular(18),
          child: Material(
            color: surface,
            child: Column(
              mainAxisSize: MainAxisSize.min,
              children: [
                Container(
                  padding: const EdgeInsets.symmetric(horizontal: 24, vertical: 18),
                  decoration: const BoxDecoration(
                    gradient: LinearGradient(
                      begin: Alignment.topLeft,
                      end: Alignment.bottomRight,
                      colors: [AppColors.primary, AppColors.primaryDark],
                    ),
                  ),
                  child: Row(
                    children: [
                      const Expanded(
                        child: Text(
                          'Izvoz ulaznica',
                          style: TextStyle(fontSize: 18, fontWeight: FontWeight.w700, color: Colors.white),
                        ),
                      ),
                      IconButton(
                        onPressed: () => Navigator.of(context).pop(),
                        icon: const Icon(Icons.close, size: 20, color: Colors.white),
                      ),
                    ],
                  ),
                ),
                Flexible(
                  child: ValueListenableBuilder<List<TicketPrintBatchResponse>>(
                    valueListenable: exportNotifications.batches,
                    builder: (context, batches, _) {
                      if (batches.isEmpty) {
                        return const Padding(
                          padding: EdgeInsets.all(28),
                          child: Text('Trenutno nema izvoza ulaznica.', textAlign: TextAlign.center),
                        );
                      }

                      return ListView.separated(
                        shrinkWrap: true,
                        padding: const EdgeInsets.all(16),
                        itemCount: batches.length,
                        separatorBuilder: (_, _) => Divider(color: border, height: 20),
                        itemBuilder: (context, index) => _ExportRow(batch: batches[index]),
                      );
                    },
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

class _ExportRow extends StatefulWidget {
  final TicketPrintBatchResponse batch;

  const _ExportRow({required this.batch});

  @override
  State<_ExportRow> createState() => _ExportRowState();
}

class _ExportRowState extends State<_ExportRow> {
  bool _isBusy = false;

  Future<void> _download() async {
    setState(() => _isBusy = true);
    await saveTicketExport(context, widget.batch);
    if (mounted) setState(() => _isBusy = false);
  }

  Future<void> _retry() async {
    setState(() => _isBusy = true);
    try {
      await TicketPrintProvider().retry(widget.batch.id);
      await exportNotifications.refresh();
    } catch (e) {
      handleApiError(e);
    }
    if (mounted) setState(() => _isBusy = false);
  }

  @override
  Widget build(BuildContext context) {
    final isDark = Theme.of(context).brightness == Brightness.dark;
    final tertiary = isDark ? AppColors.darkTextTertiary : AppColors.lightTextTertiary;
    final batch = widget.batch;

    final subtitle = switch (batch.status) {
      TicketPrintBatchStatus.queued => '${batch.ticketCount} karata · u redu čekanja',
      TicketPrintBatchStatus.rendering => '${batch.renderedCount}/${batch.ticketCount} karata · priprema PDF-a',
      TicketPrintBatchStatus.ready => '${batch.ticketCount} karata · ${batch.pageCount} stranica',
      TicketPrintBatchStatus.failed => batch.errorMessage ?? 'Izvoz nije uspio.',
    };

    return Row(
      children: [
        Expanded(
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              Text(
                batch.productName,
                style: const TextStyle(fontSize: 14, fontWeight: FontWeight.w700),
                overflow: TextOverflow.ellipsis,
              ),
              const SizedBox(height: 2),
              Text(
                subtitle,
                style: TextStyle(
                  fontSize: 12,
                  color: batch.status == TicketPrintBatchStatus.failed ? AppColors.error : tertiary,
                ),
              ),
            ],
          ),
        ),
        const SizedBox(width: 12),
        if (_isBusy)
          const SizedBox(width: 18, height: 18, child: CircularProgressIndicator(strokeWidth: 2))
        else if (batch.isDownloadable)
          FilledButton.icon(
            onPressed: _download,
            style: FilledButton.styleFrom(
              backgroundColor: AppColors.successDark,
              padding: const EdgeInsets.symmetric(horizontal: 12, vertical: 8),
              minimumSize: Size.zero,
              tapTargetSize: MaterialTapTargetSize.shrinkWrap,
            ),
            icon: const Icon(Icons.download_rounded, size: 14),
            label: const Text('Preuzmi', style: TextStyle(fontSize: 12)),
          )
        else if (batch.status == TicketPrintBatchStatus.failed)
          OutlinedButton(
            onPressed: _retry,
            style: OutlinedButton.styleFrom(
              foregroundColor: AppColors.error,
              side: const BorderSide(color: AppColors.error),
              padding: const EdgeInsets.symmetric(horizontal: 12, vertical: 8),
              minimumSize: Size.zero,
              tapTargetSize: MaterialTapTargetSize.shrinkWrap,
            ),
            child: const Text('Pokušaj ponovo', style: TextStyle(fontSize: 12)),
          )
        else
          const SizedBox(width: 18, height: 18, child: CircularProgressIndicator(strokeWidth: 2)),
      ],
    );
  }
}
