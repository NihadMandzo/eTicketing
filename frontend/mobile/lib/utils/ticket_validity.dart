import 'package:flutter/material.dart';

import '../theme/app_colors.dart';

/// Whether a ticket still gets its holder through the gate, and if not, why.
///
/// Two independent things decide this and both have to be checked: the ticket's own
/// `TicketStatus` (an organizer scanning it at the gate moves it to `Used`, terminal), and whether
/// the thing it admits you to has already happened. A ticket can be perfectly `Confirmed` and still
/// worthless because the concert was last month, and it can be scanned and spent on the morning of
/// an event that is still, by date, "upcoming". Neither signal alone answers the question a holder
/// is actually asking, which is "can I walk in with this".
enum TicketValidity {
  valid(label: 'Važeća', icon: Icons.verified_rounded),

  /// Payment hasn't settled yet, so there is nothing to admit anyone with. Rare — purchases
  /// complete on the critical path — but a `Processing` ticket must not claim to be valid.
  pending(label: 'U obradi', icon: Icons.hourglass_bottom_rounded),

  /// Scanned at the gate. This is the state the whole screen exists to make obvious.
  used(label: 'Iskorištena', icon: Icons.check_circle_rounded),

  /// The event, day or subscription period is behind us.
  expired(label: 'Istekla', icon: Icons.schedule_rounded),

  cancelled(label: 'Otkazana', icon: Icons.cancel_rounded);

  const TicketValidity({required this.label, required this.icon});

  final String label;
  final IconData icon;

  /// The one question worth asking. Everything else is the reason for the answer.
  bool get isUsable => this == TicketValidity.valid;
}

/// Resolves a ticket's validity.
///
/// [expiresAfter] is the last date the ticket is good for — `validTo` for a subscription period,
/// `validDate` for a day pass, the product's own date for a one-off event. Null means the date is
/// unknown (the product lookup failed), and an unknown date must not expire anything: a ticket
/// wrongly greyed out as expired is worse than one that leaves the question to the gate.
///
/// Status wins over date. A cancelled or already-scanned ticket is finished regardless of when the
/// event is, and saying "Istekla" about a ticket somebody already used at the door would answer
/// the wrong question.
TicketValidity resolveTicketValidity({
  required String status,
  required DateTime? expiresAfter,
  DateTime? now,
}) {
  switch (status) {
    case 'Cancelled':
      return TicketValidity.cancelled;
    case 'Used':
      return TicketValidity.used;
    case 'Processing':
      return TicketValidity.pending;
  }

  if (expiresAfter == null) return TicketValidity.valid;

  // Compared by calendar day, not instant: a ticket for tonight's show is still valid at 09:00
  // this morning, and one for a day pass is valid for the whole of its day.
  final today = now ?? DateTime.now();
  final lastValidDay = DateTime(expiresAfter.year, expiresAfter.month, expiresAfter.day);
  final startOfToday = DateTime(today.year, today.month, today.day);

  return lastValidDay.isBefore(startOfToday) ? TicketValidity.expired : TicketValidity.valid;
}

/// The two colours a validity badge is drawn in.
///
/// Three visual groups, not five: **usable** is the brand-adjacent green, **spent or lapsed** is
/// neutral grey, and **cancelled** is the error red — because a cancelled ticket is something gone
/// wrong that the holder may need to act on, while a used or expired one is simply finished. That
/// keeps the palette honest about which states deserve alarm.
({Color foreground, Color background}) ticketValidityColors(TicketValidity validity, bool isDark) {
  switch (validity) {
    case TicketValidity.valid:
      return (
        foreground: AppColors.success,
        background: isDark ? AppColors.darkSurfaceTint : AppColors.lightSurfaceTint,
      );
    case TicketValidity.cancelled:
      return (
        foreground: isDark ? AppColors.error : AppColors.errorDark,
        background: isDark ? AppColors.errorBgDarkMode : AppColors.errorBg,
      );
    case TicketValidity.pending:
    case TicketValidity.used:
    case TicketValidity.expired:
      return (
        foreground: isDark ? AppColors.darkTextTertiary : AppColors.lightTextTertiary,
        background: isDark ? AppColors.darkSurfaceMuted : AppColors.lightSurfaceMuted,
      );
  }
}

/// The validity label as a small pill — the same component on the ticket list and on the ticket
/// itself, so the two can never disagree about how a state looks.
class TicketValidityBadge extends StatelessWidget {
  final TicketValidity validity;

  /// Larger type and padding for the ticket screen, where it is the headline rather than a marker
  /// on a row.
  final bool prominent;

  const TicketValidityBadge({super.key, required this.validity, this.prominent = false});

  @override
  Widget build(BuildContext context) {
    final isDark = Theme.of(context).brightness == Brightness.dark;
    final colors = ticketValidityColors(validity, isDark);
    final fontSize = prominent ? 13.0 : 11.0;

    return Container(
      padding: EdgeInsets.symmetric(horizontal: prominent ? 12 : 8, vertical: prominent ? 7 : 4),
      decoration: BoxDecoration(
        color: colors.background,
        borderRadius: BorderRadius.circular(prominent ? 10 : 6),
        // Colour alone can't be the only carrier — the greys are close in value and the badge is
        // small. The border plus the icon give it two more ways to read.
        border: Border.all(color: colors.foreground.withValues(alpha: 0.35)),
      ),
      child: Row(
        mainAxisSize: MainAxisSize.min,
        children: [
          Icon(validity.icon, size: prominent ? 15 : 12, color: colors.foreground),
          SizedBox(width: prominent ? 7 : 5),
          Text(
            validity.label,
            style: TextStyle(fontSize: fontSize, fontWeight: FontWeight.w700, color: colors.foreground),
          ),
        ],
      ),
    );
  }
}
