import 'package:flutter/material.dart';

import '../../../theme/app_colors.dart';

/// One column of a report table. [flex] mirrors the fractional grid columns of
/// the design — 2.2fr for the name column, 1fr for the numeric ones. The point
/// at which the table stops shrinking and scrolls instead is a property of the
/// table as a whole, not of any one column: see [ReportDataTable.minWidth].
class ReportColumn {
  final String header;
  final int flex;
  final bool rightAligned;

  const ReportColumn(this.header, {this.flex = 10, this.rightAligned = false});
}

/// A cell. Text is the common case; [widget] is for the badge and progress-bar
/// cells the design uses for status and occupancy.
class ReportCell {
  final String? text;
  final Widget? widget;
  final Color? color;
  final bool bold;

  const ReportCell(this.text, {this.color, this.bold = false}) : widget = null;

  const ReportCell.custom(this.widget)
      : text = null,
        color = null,
        bold = false;
}

/// The uppercase-header, hairline-ruled table the three report tabs share.
///
/// Not Flutter's `DataTable`: that one sizes every column to its widest cell,
/// which makes a table of a dozen products jump about as the data changes, and
/// it has no fractional column widths. This is the design's CSS grid rendered
/// literally — fixed proportions, so the columns line up across rows and across
/// reloads.
class ReportDataTable extends StatelessWidget {
  final List<ReportColumn> columns;
  final List<List<ReportCell>> rows;

  /// The design's bold "Ukupno" footer, drawn above a heavier rule.
  final List<ReportCell>? totalsRow;

  /// Shown in place of the rows when there are none.
  final String emptyMessage;

  /// Total width below which the table scrolls horizontally instead of
  /// squeezing. Six numeric columns plus a name stop being readable well before
  /// the window itself gets narrow.
  final double minWidth;

  const ReportDataTable({
    super.key,
    required this.columns,
    required this.rows,
    this.totalsRow,
    this.emptyMessage = 'Nema podataka za odabrani period.',
    this.minWidth = 720,
  });

  @override
  Widget build(BuildContext context) {
    final brightness = Theme.of(context).brightness;

    return LayoutBuilder(
      builder: (context, constraints) {
        final width = constraints.maxWidth < minWidth ? minWidth : constraints.maxWidth;
        final table = SizedBox(
          width: width,
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.start,
            mainAxisSize: MainAxisSize.min,
            children: [
              _HeaderRow(columns: columns, brightness: brightness),
              if (rows.isEmpty)
                Padding(
                  padding: const EdgeInsets.symmetric(vertical: 28),
                  child: Text(
                    emptyMessage,
                    style: TextStyle(fontSize: 13, color: AppColors.textTertiary(brightness)),
                  ),
                )
              else
                for (final row in rows)
                  _BodyRow(columns: columns, cells: row, brightness: brightness),
              if (totalsRow != null && rows.isNotEmpty)
                _BodyRow(
                  columns: columns,
                  cells: totalsRow!,
                  brightness: brightness,
                  isTotals: true,
                ),
            ],
          ),
        );

        return constraints.maxWidth < minWidth
            ? SingleChildScrollView(scrollDirection: Axis.horizontal, child: table)
            : table;
      },
    );
  }
}

class _HeaderRow extends StatelessWidget {
  final List<ReportColumn> columns;
  final Brightness brightness;

  const _HeaderRow({required this.columns, required this.brightness});

  @override
  Widget build(BuildContext context) {
    return Container(
      padding: const EdgeInsets.only(bottom: 8),
      decoration: BoxDecoration(
        border: Border(bottom: BorderSide(color: AppColors.border(brightness))),
      ),
      child: Row(
        children: [
          for (final column in columns)
            Expanded(
              flex: column.flex,
              child: Padding(
                padding: const EdgeInsets.only(right: 10),
                child: Text(
                  column.header.toUpperCase(),
                  textAlign: column.rightAligned ? TextAlign.right : TextAlign.left,
                  maxLines: 1,
                  overflow: TextOverflow.ellipsis,
                  style: TextStyle(
                    fontSize: 11,
                    fontWeight: FontWeight.w600,
                    letterSpacing: 0.4,
                    color: AppColors.textTertiary(brightness),
                  ),
                ),
              ),
            ),
        ],
      ),
    );
  }
}

class _BodyRow extends StatelessWidget {
  final List<ReportColumn> columns;
  final List<ReportCell> cells;
  final Brightness brightness;
  final bool isTotals;

  const _BodyRow({
    required this.columns,
    required this.cells,
    required this.brightness,
    this.isTotals = false,
  });

  @override
  Widget build(BuildContext context) {
    final isDark = brightness == Brightness.dark;

    return Container(
      padding: const EdgeInsets.symmetric(vertical: 12),
      decoration: BoxDecoration(
        border: Border(
          top: isTotals ? BorderSide(color: AppColors.border(brightness)) : BorderSide.none,
          bottom: isTotals
              ? BorderSide.none
              : BorderSide(
                  color: isDark ? AppColors.darkSurfaceMuted : AppColors.lightSurfaceMuted,
                ),
        ),
      ),
      child: Row(
        crossAxisAlignment: CrossAxisAlignment.center,
        children: [
          for (var i = 0; i < columns.length; i++)
            Expanded(
              flex: columns[i].flex,
              child: Padding(
                padding: const EdgeInsets.only(right: 10),
                child: Align(
                  alignment: columns[i].rightAligned ? Alignment.centerRight : Alignment.centerLeft,
                  // A row shorter than the header would be a caller bug; the
                  // cell renders blank rather than throwing mid-build and
                  // taking the whole screen down.
                  child: i < cells.length
                      ? _cell(cells[i], columns[i])
                      : const SizedBox.shrink(),
                ),
              ),
            ),
        ],
      ),
    );
  }

  Widget _cell(ReportCell cell, ReportColumn column) {
    if (cell.widget != null) return cell.widget!;

    return Text(
      cell.text ?? '',
      textAlign: column.rightAligned ? TextAlign.right : TextAlign.left,
      maxLines: 2,
      overflow: TextOverflow.ellipsis,
      style: TextStyle(
        fontSize: 13,
        fontWeight: (isTotals || cell.bold) ? FontWeight.w700 : FontWeight.w400,
        color: cell.color ?? AppColors.textSecondary(brightness),
      ),
    );
  }
}

/// The two-line first cell of the product and organization tables: a name over
/// a smaller grey detail line.
class ReportNameCell extends StatelessWidget {
  final String name;
  final String? meta;

  const ReportNameCell({super.key, required this.name, this.meta});

  @override
  Widget build(BuildContext context) {
    final brightness = Theme.of(context).brightness;

    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      mainAxisSize: MainAxisSize.min,
      children: [
        Text(
          name,
          maxLines: 1,
          overflow: TextOverflow.ellipsis,
          style: TextStyle(
            fontSize: 13,
            fontWeight: FontWeight.w600,
            color: AppColors.textPrimary(brightness),
          ),
        ),
        if (meta != null && meta!.isNotEmpty) ...[
          const SizedBox(height: 2),
          Text(
            meta!,
            maxLines: 1,
            overflow: TextOverflow.ellipsis,
            style: TextStyle(fontSize: 11, color: AppColors.textTertiary(brightness)),
          ),
        ],
      ],
    );
  }
}

/// The pill the design uses for a check-in rate and an organization's status.
class ReportBadge extends StatelessWidget {
  final String label;
  final Color color;

  const ReportBadge({super.key, required this.label, required this.color});

  @override
  Widget build(BuildContext context) {
    return Container(
      padding: const EdgeInsets.symmetric(horizontal: 10, vertical: 3),
      decoration: BoxDecoration(
        color: color.withValues(alpha: Theme.of(context).brightness == Brightness.dark ? 0.2 : 0.12),
        borderRadius: BorderRadius.circular(20),
      ),
      child: Text(
        label,
        maxLines: 1,
        overflow: TextOverflow.ellipsis,
        style: TextStyle(fontSize: 12, fontWeight: FontWeight.w600, color: color),
      ),
    );
  }
}

/// The occupancy cell: a thin track with a filled portion, and the percentage
/// beside it.
class ReportProgressCell extends StatelessWidget {
  final double? percent;
  final Color color;
  final String label;

  const ReportProgressCell({
    super.key,
    required this.percent,
    required this.color,
    required this.label,
  });

  @override
  Widget build(BuildContext context) {
    final brightness = Theme.of(context).brightness;
    final isDark = brightness == Brightness.dark;

    if (percent == null) {
      return Text(label, style: TextStyle(fontSize: 13, color: AppColors.textTertiary(brightness)));
    }

    return Row(
      mainAxisSize: MainAxisSize.min,
      children: [
        Flexible(
          child: Container(
            constraints: const BoxConstraints(maxWidth: 70),
            height: 6,
            decoration: BoxDecoration(
              color: isDark ? AppColors.darkSurfaceMuted : AppColors.lightSurfaceMuted,
              borderRadius: BorderRadius.circular(3),
            ),
            child: FractionallySizedBox(
              alignment: Alignment.centerLeft,
              widthFactor: (percent! / 100).clamp(0.0, 1.0),
              child: Container(
                decoration: BoxDecoration(
                  color: color,
                  borderRadius: BorderRadius.circular(3),
                ),
              ),
            ),
          ),
        ),
        const SizedBox(width: 8),
        Text(label, style: TextStyle(fontSize: 12, color: AppColors.textTertiary(brightness))),
      ],
    );
  }
}
