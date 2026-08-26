import 'dart:math' as math;

import 'package:flutter/material.dart';

import '../../core/formatting.dart';
import '../../models/enums/ticketing_mode.dart';

/// The two faces the printed ticket is typeset in, bundled into this app so the
/// preview is set in the same type as the paper (see `pubspec.yaml`).
const _sansFamily = 'Manrope';
const _monoFamily = 'IBMPlexMono';

/// The sheet's palette, transcribed from `TicketTheme`. Deliberately not read
/// from the app's own palette: these are ink values on paper, and if the app
/// ever rebrands its chrome the printed ticket must not silently follow.
class _Ink {
  const _Ink._();

  static const greenDark = Color(0xFF1D5B3A);
  static const greenMid = Color(0xFF6FAE4A);
  static const greenLight = Color(0xFF8DC63F);
  static const textPrimary = Color(0xFF111827);
  static const textMuted = Color(0xFF6B7280);
  static const border = Color(0xFFE5E7EB);
  static const borderStrong = Color(0xFFD1D5DB);
  static const surface = Color(0xFFF5F5F5);
  static const stubSurface = Color(0xFFF9FAFB);
}

/// Design pixels to logical pixels, for one rendered sheet.
///
/// `PrintSheetDocument` is authored in the mock-up's CSS pixels and converts
/// them to PDF points through `TicketTheme.Px` (x 0.75). A4 is 595.28 x 841.89
/// points, so the same sheet measures 793.7 x 1122.5 of those pixels. Holding
/// this preview in the identical units is what makes it a transcription rather
/// than a lookalike: `r(22)` here is `Px(22)` there, and the two files can be
/// read side by side.
class _Ruler {
  final double scale;

  const _Ruler(this.scale);

  double call(double designPx) => designPx * scale;

  /// Hairlines have to survive the scale-down — at 45% a 1px rule would
  /// otherwise disappear, and a border that vanishes is a layout difference.
  double hairline(double designPx) => math.max(0.5, designPx * scale);
}

/// `TicketTheme.Label` — the small-caps line above almost every value.
TextStyle _sheetLabel(
  _Ruler r,
  double sizePx, {
  Color color = _Ink.textMuted,
  double letterSpacing = 0.14,
  bool strong = false,
}) =>
    TextStyle(
      fontFamily: _sansFamily,
      fontWeight: strong ? FontWeight.w700 : FontWeight.w600,
      fontSize: r(sizePx),
      // QuestPDF states letter spacing as a factor of the font size; Flutter
      // wants it in absolute pixels.
      letterSpacing: r(sizePx) * letterSpacing,
      color: color,
    );

/// `TicketTheme.Value`.
TextStyle _sheetValue(_Ruler r, double sizePx, {Color color = _Ink.textPrimary}) =>
    TextStyle(fontFamily: _sansFamily, fontWeight: FontWeight.w700, fontSize: r(sizePx), color: color);

/// `TicketTheme.Title`.
TextStyle _sheetTitle(_Ruler r, double sizePx) =>
    TextStyle(fontFamily: _sansFamily, fontWeight: FontWeight.w800, fontSize: r(sizePx), color: _Ink.textPrimary);

/// `TicketTheme.Body`.
TextStyle _sheetBody(_Ruler r, double sizePx, double lineHeight, Color color) => TextStyle(
      fontFamily: _sansFamily,
      fontWeight: FontWeight.w400,
      fontSize: r(sizePx),
      height: lineHeight,
      color: color,
    );

/// `TicketTheme.MonoStyle`.
TextStyle _sheetMono(_Ruler r, double sizePx, {Color color = _Ink.textPrimary, bool strong = true}) => TextStyle(
      fontFamily: _monoFamily,
      fontWeight: strong ? FontWeight.w600 : FontWeight.w400,
      fontSize: r(sizePx),
      letterSpacing: r(sizePx) * 0.02,
      color: color,
    );

/// One ticket as it will appear on the sheet.
class PrintPreviewTicket {
  final String sector;

  /// Null when the sector has no ticket types — the renderer then prints the
  /// sector name on its own.
  final String? type;
  final double price;
  final int serial;

  const PrintPreviewTicket({
    required this.sector,
    required this.type,
    required this.price,
    required this.serial,
  });

  /// `PrintSheetDocument.SectorLine`.
  String get sectorLine => type == null ? sector : '$sector · $type';
}

/// Everything on the sheet that belongs to the run rather than to one ticket —
/// the preview's counterpart to `PrintSheetModel`.
class PrintSheetContext {
  final String productName;
  final String city;
  final DateTime? productDate;
  final TicketingMode ticketingMode;
  final DateTime? validDate;
  final DateTime issuedAt;

  const PrintSheetContext({
    required this.productName,
    required this.city,
    required this.productDate,
    required this.ticketingMode,
    required this.validDate,
    required this.issuedAt,
  });

  /// `PrintSheetDocument.ValidityLine`.
  String get validity {
    if (ticketingMode == TicketingMode.dailyEntry) {
      return validDate == null ? 'Datum nije određen' : formatDate(validDate!);
    }

    final date = productDate;
    return date == null
        ? 'Datum nije određen'
        : '${formatDate(date)} ${formatTime(date)}';
  }

  /// `PrintSheetDocument.EntryBadgeText`.
  String get entryBadge => ticketingMode == TicketingMode.dailyEntry ? 'DNEVNI ULAZ' : 'JEDAN ULAZ';
}

/// One A4 sheet at true proportion, laid out slot for slot against
/// `PrintSheetDocument`: three tickets, the guillotine guides between them, and
/// blank paper where the run does not fill the page.
///
/// White in both themes on purpose. This is paper, and a print preview that
/// tints itself to match the app chrome misrepresents the artifact — every
/// print dialog worth trusting shows a white page on a darker ground.
class TicketSheetPreview extends StatelessWidget {
  final PrintSheetContext sheet;
  final List<PrintPreviewTicket> tickets;
  final int ticketsPerSheet;

  const TicketSheetPreview({
    super.key,
    required this.sheet,
    required this.tickets,
    required this.ticketsPerSheet,
  });

  /// A4 portrait.
  static const a4Ratio = 210 / 297;

  /// A4's width in the design's CSS pixels — 595.28 pt / 0.75. See [_Ruler].
  static const _sheetWidth = 793.7;

  /// The tear-off stub is a constant 62 mm of the 210 mm sheet.
  static const _stubWidth = 234.33;

  @override
  Widget build(BuildContext context) {
    final isDark = Theme.of(context).brightness == Brightness.dark;

    return LayoutBuilder(
      builder: (context, constraints) {
        final r = _Ruler(constraints.maxWidth / _sheetWidth);

        return DecoratedBox(
          decoration: BoxDecoration(
            color: Colors.white,
            border: Border.all(color: isDark ? const Color(0xFF2A3B36) : _Ink.border),
            boxShadow: [
              BoxShadow(
                color: Colors.black.withValues(alpha: isDark ? 0.45 : 0.13),
                blurRadius: r(34),
                offset: Offset(0, r(12)),
              ),
            ],
          ),
          child: ClipRect(
            child: Column(
              children: [
                for (var slot = 0; slot < ticketsPerSheet; slot++) ...[
                  Expanded(
                    child: slot < tickets.length
                        ? _SheetTicket(ticket: tickets[slot], sheet: sheet, stubWidth: _stubWidth, r: r)
                        : _EmptySlot(r: r, showHint: slot == 0 && tickets.isEmpty),
                  ),
                  // The renderer draws this same 4-on/4-off rule under every
                  // ticket but the last, where the paper edge is the cut.
                  if (slot != ticketsPerSheet - 1)
                    SizedBox(
                      height: r.hairline(2),
                      child: CustomPaint(
                        painter: _DashedLinePainter(color: _Ink.borderStrong, dash: r(4)),
                        size: Size.infinite,
                      ),
                    ),
                ],
              ],
            ),
          ),
        );
      },
    );
  }
}

/// A slot the run does not reach. Only the first one speaks — an empty sheet is
/// an invitation to act, not the same sentence printed three times.
class _EmptySlot extends StatelessWidget {
  final _Ruler r;
  final bool showHint;

  const _EmptySlot({required this.r, required this.showHint});

  @override
  Widget build(BuildContext context) {
    if (!showHint) return const SizedBox.shrink();

    return Center(
      child: Padding(
        padding: EdgeInsets.symmetric(horizontal: r(60)),
        child: Text(
          'Odaberite sektor i broj karata.',
          textAlign: TextAlign.center,
          style: _sheetBody(r, 16, 1.4, const Color(0xFF9CA3AF)),
        ),
      ),
    );
  }
}

/// One printed ticket, composed in the same order as
/// `PrintSheetDocument.ComposeTicket`: the body, the dashed tear-off rule, and
/// the 62 mm QR stub.
class _SheetTicket extends StatelessWidget {
  final PrintPreviewTicket ticket;
  final PrintSheetContext sheet;
  final double stubWidth;
  final _Ruler r;

  const _SheetTicket({
    required this.ticket,
    required this.sheet,
    required this.stubWidth,
    required this.r,
  });

  /// Word for word from `PrintSheetDocument.ComposeDisclaimer`, minus its
  /// closing contact sentence — the support address is server-side
  /// configuration this screen holds no copy of.
  static const _disclaimer = 'Ulaznica važi za jedan ulaz i poništava se u trenutku skeniranja. Nije '
      'prenosiva i ne podliježe povratu novca; preprodaja iznad nominalne cijene je zabranjena. '
      'Ulazak podrazumijeva pristanak na sigurnosnu provjeru i kućni red objekta.';

  /// The renderer prints the ticket's full id here, for the gate's manual-entry
  /// fallback. Nothing is issued until the export is submitted, so the line
  /// holds the shape of an id rather than a plausible-looking one — an invented
  /// GUID on a preview is exactly the detail someone later tries to type in.
  static const _idPlaceholder = '00000000-0000-0000-0000-000000000000';

  @override
  Widget build(BuildContext context) => Row(
        children: [
          Expanded(child: _body()),
          SizedBox(
            width: r.hairline(2),
            child: CustomPaint(
              painter: _DashedLinePainter(color: _Ink.border, dash: r(4), vertical: true),
              size: Size.infinite,
            ),
          ),
          SizedBox(width: r(stubWidth), child: _stub()),
        ],
      );

  Widget _body() => Column(
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: [
          _brandBar(),
          Padding(padding: EdgeInsets.fromLTRB(r(22), r(16), r(22), 0), child: _eventTitle()),
          Padding(padding: EdgeInsets.fromLTRB(r(22), r(14), r(22), 0), child: _facts()),
          // `Extend().AlignBottom()` in the document: the disclaimer sinks to
          // the foot of the ticket however much room the title took above it.
          Expanded(
            child: Padding(
              padding: EdgeInsets.fromLTRB(r(22), r(6), r(22), r(14)),
              child: Column(mainAxisAlignment: MainAxisAlignment.end, children: [_disclaimerRow()]),
            ),
          ),
        ],
      );

  Widget _brandBar() => Container(
        color: _Ink.greenDark,
        padding: EdgeInsets.symmetric(horizontal: r(22), vertical: r(8)),
        child: Row(
          children: [
            Container(
              width: r(64),
              height: r(64),
              decoration: const BoxDecoration(color: Colors.white, shape: BoxShape.circle),
              alignment: Alignment.center,
              child: Image.asset('assets/ticket-mark.png', width: r(41), height: r(42), fit: BoxFit.contain),
            ),
            const Spacer(),
            Flexible(
              child: Text(
                'ULAZNICA · EKARTA',
                textAlign: TextAlign.right,
                maxLines: 1,
                overflow: TextOverflow.ellipsis,
                style: _sheetLabel(r, 10, color: _Ink.greenLight, letterSpacing: 0.16),
              ),
            ),
          ],
        ),
      );

  Widget _eventTitle() => Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Text('DOGAĐAJ',
              maxLines: 1,
              overflow: TextOverflow.ellipsis,
              style: _sheetLabel(r, 10, letterSpacing: 0.16)),
          Padding(
            padding: EdgeInsets.only(top: r(4)),
            child: Text(
              sheet.productName,
              maxLines: 2,
              overflow: TextOverflow.ellipsis,
              style: _sheetTitle(r, 25).copyWith(letterSpacing: r(25) * -0.02, height: 1.12),
            ),
          ),
        ],
      );

  /// The document's 3-column fact grid (1fr / 1.6fr / 0.75fr) over a 1fr /
  /// 2.35fr row, at the same 18px gutter and 12px row gap.
  Widget _facts() => Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Row(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              Expanded(flex: 100, child: _fact('LOKACIJA', sheet.city)),
              SizedBox(width: r(18)),
              Expanded(flex: 160, child: _fact('SEKTOR', ticket.sectorLine)),
              SizedBox(width: r(18)),
              Expanded(
                flex: 75,
                child: _fact('CIJENA', formatMoney(ticket.price), color: _Ink.greenDark),
              ),
            ],
          ),
          SizedBox(height: r(12)),
          Row(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              Expanded(
                flex: 100,
                child: _fact('DATUM IZDAVANJA', formatDate(sheet.issuedAt)),
              ),
              SizedBox(width: r(18)),
              Expanded(
                flex: 235,
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    Text('VRIJEDI', maxLines: 1, overflow: TextOverflow.ellipsis, style: _sheetLabel(r, 9.5)),
                    Padding(
                      padding: EdgeInsets.only(top: r(3)),
                      child: Row(
                        children: [
                          Flexible(
                            child: Text(
                              sheet.validity,
                              maxLines: 1,
                              overflow: TextOverflow.ellipsis,
                              style: _sheetValue(r, 14),
                            ),
                          ),
                          SizedBox(width: r(8)),
                          _entryBadge(),
                        ],
                      ),
                    ),
                  ],
                ),
              ),
            ],
          ),
        ],
      );

  Widget _fact(String label, String value, {Color color = _Ink.textPrimary}) => Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Text(label, maxLines: 1, overflow: TextOverflow.ellipsis, style: _sheetLabel(r, 9.5)),
          Padding(
            padding: EdgeInsets.only(top: r(3)),
            child: Text(
              value,
              maxLines: 2,
              overflow: TextOverflow.ellipsis,
              style: _sheetValue(r, 14, color: color),
            ),
          ),
        ],
      );

  Widget _entryBadge() => Container(
        decoration: BoxDecoration(
          color: _Ink.surface,
          border: Border.all(color: _Ink.border, width: r.hairline(1)),
        ),
        padding: EdgeInsets.symmetric(horizontal: r(8), vertical: r(3)),
        child: Text(
          sheet.entryBadge,
          maxLines: 1,
          style: _sheetLabel(r, 9.5, color: _Ink.greenDark, letterSpacing: 0.1, strong: true),
        ),
      );

  Widget _disclaimerRow() => Row(
        crossAxisAlignment: CrossAxisAlignment.end,
        children: [
          Expanded(
            child: Align(
              alignment: Alignment.bottomLeft,
              // 106 mm, as in the document — the text stops well before the
              // brand bar rather than running under it.
              child: ConstrainedBox(
                constraints: BoxConstraints(maxWidth: r(400.6)),
                child: Text(
                  _disclaimer,
                  // The renderer lets this find its own height; the clamp is a
                  // guard so a metric difference can never burst the slot.
                  maxLines: 5,
                  overflow: TextOverflow.ellipsis,
                  style: _sheetBody(r, 8.5, 1.45, _Ink.textMuted),
                ),
              ),
            ),
          ),
          SizedBox(width: r(16)),
          Row(
            mainAxisSize: MainAxisSize.min,
            children: [
              for (final color in const [_Ink.greenDark, _Ink.greenMid, _Ink.greenLight])
                Container(width: r(64 / 3), height: r(6), color: color),
            ],
          ),
        ],
      );

  Widget _stub() => Container(
        color: _Ink.stubSurface,
        padding: EdgeInsets.all(r(14)),
        child: Column(
          mainAxisAlignment: MainAxisAlignment.center,
          children: [
            Container(
              color: Colors.white,
              foregroundDecoration: BoxDecoration(border: Border.all(color: _Ink.border, width: r.hairline(1))),
              padding: EdgeInsets.all(r(7)),
              child: SizedBox(
                width: r(124),
                height: r(124),
                child: CustomPaint(painter: _QrPreviewPainter(seed: ticket.serial)),
              ),
            ),
            SizedBox(height: r(9)),
            Text('SERIJSKI BROJ', maxLines: 1, style: _sheetLabel(r, 9)),
            Padding(
              padding: EdgeInsets.only(top: r(3)),
              child: Text(formatStubNumber(ticket.serial), style: _sheetMono(r, 11.5)),
            ),
            Padding(
              padding: EdgeInsets.only(top: r(2)),
              child: Text(_idPlaceholder, style: _sheetMono(r, 7, color: _Ink.textMuted, strong: false)),
            ),
            SizedBox(height: r(9)),
            ConstrainedBox(
              // 50 mm.
              constraints: BoxConstraints(maxWidth: r(189)),
              child: Text(
                'Skenirajte na ulazu. Ne dijelite ovaj kod.',
                textAlign: TextAlign.center,
                style: _sheetBody(r, 8.5, 1.4, _Ink.textMuted),
              ),
            ),
          ],
        ),
      );
}

class _DashedLinePainter extends CustomPainter {
  final Color color;
  final double dash;
  final bool vertical;

  const _DashedLinePainter({required this.color, required this.dash, this.vertical = false});

  @override
  void paint(Canvas canvas, Size size) {
    final paint = Paint()
      ..color = color
      ..strokeWidth = vertical ? size.width : size.height
      ..strokeCap = StrokeCap.square;

    final length = vertical ? size.height : size.width;
    final step = math.max(1.0, dash * 2);

    for (var offset = 0.0; offset < length; offset += step) {
      final end = math.min(offset + dash, length);
      canvas.drawLine(
        vertical ? Offset(size.width / 2, offset) : Offset(offset, size.height / 2),
        vertical ? Offset(size.width / 2, end) : Offset(end, size.height / 2),
        paint,
      );
    }
  }

  @override
  bool shouldRepaint(_DashedLinePainter old) =>
      old.color != color || old.dash != dash || old.vertical != vertical;
}

/// A stand-in for the printed QR: the real finder, timing and alignment
/// patterns of a version-6 symbol around a field of noise seeded from the stub
/// number, so each ticket on the sheet visibly carries a different code at the
/// density the printed one will have.
///
/// Not scannable, and not meant to be. The live payload is HMAC-signed by
/// Ticketing when the batch is created, which is after this screen is done —
/// drawing a believable block is the honest way to show the layout without
/// implying this particular code is a working ticket.
class _QrPreviewPainter extends CustomPainter {
  final int seed;

  const _QrPreviewPainter({required this.seed});

  /// 41 x 41 is what an `ETK1.{id}.{signature}` payload encodes to at error
  /// correction level Q, and QRCoder surrounds it with the standard 4-module
  /// quiet zone.
  static const _modules = 41;
  static const _quiet = 4;

  @override
  void paint(Canvas canvas, Size size) {
    final unit = size.width / (_modules + _quiet * 2);
    final ink = Paint()..color = _Ink.textPrimary;
    final random = math.Random(seed);

    void module(int row, int col) => canvas.drawRect(
          Rect.fromLTWH((col + _quiet) * unit, (row + _quiet) * unit, unit, unit),
          ink,
        );

    void square(int row, int col, int span) {
      final centre = span ~/ 2;
      final coreReach = span ~/ 6;
      for (var dr = 0; dr < span; dr++) {
        for (var dc = 0; dc < span; dc++) {
          final edge = dr == 0 || dr == span - 1 || dc == 0 || dc == span - 1;
          final core = (dr - centre).abs() <= coreReach && (dc - centre).abs() <= coreReach;
          if (edge || core) module(row + dr, col + dc);
        }
      }
    }

    // Everything the standard reserves: the three finders with their
    // separators, both timing lines, and the lone alignment pattern.
    bool reserved(int row, int col) =>
        (row < 9 && col < 9) ||
        (row < 9 && col >= _modules - 8) ||
        (row >= _modules - 8 && col < 9) ||
        row == 6 ||
        col == 6 ||
        (row >= _modules - 9 && row <= _modules - 5 && col >= _modules - 9 && col <= _modules - 5);

    for (var row = 0; row < _modules; row++) {
      for (var col = 0; col < _modules; col++) {
        if (!reserved(row, col) && random.nextBool()) module(row, col);
      }
    }

    for (var i = 8; i < _modules - 8; i++) {
      if (i.isEven) {
        module(6, i);
        module(i, 6);
      }
    }

    square(0, 0, 7);
    square(0, _modules - 7, 7);
    square(_modules - 7, 0, 7);
    square(_modules - 9, _modules - 9, 5);
  }

  @override
  bool shouldRepaint(_QrPreviewPainter old) => old.seed != seed;
}
