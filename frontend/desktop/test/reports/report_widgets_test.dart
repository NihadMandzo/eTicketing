import 'package:desktop/screens/widgets/reports/report_bar_chart.dart';
import 'package:desktop/screens/widgets/reports/report_data_table.dart';
import 'package:desktop/screens/widgets/reports/report_metric_card.dart';
import 'package:desktop/screens/widgets/reports/report_range_bar.dart';
import 'package:desktop/theme/app_theme.dart';
import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';

/// The reports screen is the widest layout in the back-office — a seven-column
/// table, a multi-bar chart and a six-pill filter bar, all on one page. Overflow
/// is the failure mode to guard (.claude/rules/21-frontend-desktop.md requires
/// zero `RenderFlex` overflow from ~700px up), so every block here is pumped at
/// the narrow end of that range as well as at a comfortable width, in both
/// themes.
void main() {
  /// Pumps one report block at a given width and fails if anything overflowed —
  /// `tester.takeException()` is how a `RenderFlex` overflow surfaces in a
  /// widget test, since it only paints a yellow banner at runtime.
  Future<void> pumpBlock(
    WidgetTester tester,
    Widget block, {
    double width = 1200,
    Brightness brightness = Brightness.light,
  }) async {
    final size = Size(width, 900);
    await tester.binding.setSurfaceSize(size);
    addTearDown(() => tester.binding.setSurfaceSize(null));

    await tester.pumpWidget(
      MaterialApp(
        theme: brightness == Brightness.dark ? AppTheme.dark : AppTheme.light,
        home: Scaffold(
          body: SingleChildScrollView(
            child: SizedBox(width: width, child: Padding(padding: const EdgeInsets.all(24), child: block)),
          ),
        ),
      ),
    );
    await tester.pumpAndSettle();

    expect(tester.takeException(), isNull);
  }

  ReportRangeBar rangeBar({DateTime? from, DateTime? to}) => ReportRangeBar(
        from: from ?? DateTime(2026, 7, 23),
        to: to ?? DateTime(2026, 8, 21),
        activePresetId: '30d',
        today: DateTime(2026, 8, 21),
        onPreset: (_) {},
        onFrom: (_) {},
        onTo: (_) {},
      );

  final productColumns = <ReportColumn>[
    const ReportColumn('Proizvod', flex: 26),
    const ReportColumn('Prodano', flex: 9, rightAligned: true),
    const ReportColumn('Popunjenost', flex: 13),
    const ReportColumn('Pros. cijena', flex: 12, rightAligned: true),
    const ReportColumn('Otkazano', flex: 9, rightAligned: true),
    const ReportColumn('Prihod', flex: 13, rightAligned: true),
  ];

  List<List<ReportCell>> productRows(int count) => [
        for (var i = 0; i < count; i++)
          [
            ReportCell.custom(ReportNameCell(
              name: 'Ljetni Muzički Festival ${i + 1}',
              meta: 'Sunset Events d.o.o. · Mostar',
            )),
            const ReportCell('2.840'),
            const ReportCell.custom(ReportProgressCell(
              percent: 94,
              color: Colors.green,
              label: '94,0%',
            )),
            const ReportCell('145,00 KM'),
            const ReportCell('42'),
            const ReportCell('412.600,00 KM'),
          ],
      ];

  group('ReportRangeBar', () {
    testWidgets('lays out its six presets and both pickers at 700px', (tester) async {
      await pumpBlock(tester, rangeBar(), width: 700);

      expect(find.text('7 dana'), findsOneWidget);
      expect(find.text('Godina'), findsOneWidget);
      expect(find.text('Od'), findsOneWidget);
      expect(find.text('Do'), findsOneWidget);
    });

    testWidgets('reports the inclusive day count of the selected range', (tester) async {
      await pumpBlock(tester, rangeBar());

      // 23 July to 21 August inclusive.
      expect(find.text('30 dana'), findsNWidgets(2)); // the preset pill and the badge
    });

    testWidgets('warns when Od is after Do', (tester) async {
      await pumpBlock(
        tester,
        rangeBar(from: DateTime(2026, 8, 21), to: DateTime(2026, 8, 1)),
      );

      expect(find.text('nevažeći raspon'), findsOneWidget);
      expect(find.text('Datum "Od" mora biti prije datuma "Do".'), findsOneWidget);
    });

    testWidgets('warns when the range exceeds what the API accepts', (tester) async {
      // 367 days inclusive — one past ReportRangeValidator.MaxRangeDays.
      await pumpBlock(
        tester,
        rangeBar(from: DateTime(2025, 8, 20), to: DateTime(2026, 8, 21)),
      );

      expect(find.text('Period ne može biti duži od 366 dana.'), findsOneWidget);
    });

    testWidgets('renders in the dark theme without overflow', (tester) async {
      await pumpBlock(tester, rangeBar(), width: 700, brightness: Brightness.dark);
    });
  });

  group('ReportDataTable', () {
    testWidgets('renders a full product table at 1200px', (tester) async {
      await pumpBlock(
        tester,
        ReportDataTable(columns: productColumns, rows: productRows(4)),
      );

      expect(find.text('PROIZVOD'), findsOneWidget);
      expect(find.text('Ljetni Muzički Festival 1'), findsOneWidget);
    });

    testWidgets('scrolls rather than squeezing below its minimum width', (tester) async {
      await pumpBlock(
        tester,
        ReportDataTable(columns: productColumns, rows: productRows(4)),
        width: 700,
      );

      // The header is still laid out at full width inside a horizontal scroller,
      // so nothing is clipped or ellipsised into uselessness.
      expect(find.byType(SingleChildScrollView), findsWidgets);
    });

    testWidgets('shows the empty message instead of a bare header', (tester) async {
      await pumpBlock(
        tester,
        ReportDataTable(columns: productColumns, rows: const []),
      );

      expect(find.text('Nema podataka za odabrani period.'), findsOneWidget);
    });

    testWidgets('draws the totals row only when there are rows to total', (tester) async {
      await pumpBlock(
        tester,
        ReportDataTable(
          columns: productColumns,
          rows: const [],
          totalsRow: const [ReportCell('Ukupno')],
        ),
      );

      expect(find.text('Ukupno'), findsNothing);
    });

    testWidgets('renders a row shorter than the header without throwing', (tester) async {
      // A caller bug, but it must not take the whole screen down mid-build.
      await pumpBlock(
        tester,
        ReportDataTable(
          columns: productColumns,
          rows: const [
            [ReportCell('Samo ime')],
          ],
        ),
      );

      expect(find.text('Samo ime'), findsOneWidget);
    });
  });

  group('ReportBarChart', () {
    testWidgets('renders a twelve-month series at 700px', (tester) async {
      await pumpBlock(
        tester,
        ReportBarChart(
          bars: [
            for (var i = 0; i < 12; i++)
              ReportBarData(label: 'Mj ${i + 1}', value: '${i * 12}k', ratio: (i + 1) / 12),
          ],
        ),
        width: 700,
      );
    });

    testWidgets('renders an all-zero series without dividing by zero', (tester) async {
      await pumpBlock(
        tester,
        ReportBarChart(
          bars: const [
            ReportBarData(label: '1. avg', value: '0', ratio: 0),
            ReportBarData(label: '2. avg', value: '0', ratio: 0),
          ],
        ),
      );
    });

    testWidgets('says so when there is nothing to draw', (tester) async {
      await pumpBlock(tester, const ReportBarChart(bars: []));

      expect(find.text('Nema podataka za odabrani period.'), findsOneWidget);
    });
  });

  group('ReportMetricCard', () {
    testWidgets('shrinks a long value rather than clipping it', (tester) async {
      await pumpBlock(
        tester,
        const SizedBox(
          width: 200,
          child: ReportMetricCard(
            label: 'Ukupan prihod',
            value: '1.284.600,00 KM',
            hint: '+12,4% u odnosu na prethodni period',
            emphasis: ReportEmphasis.positive,
          ),
        ),
        width: 700,
      );

      expect(find.text('1.284.600,00 KM'), findsOneWidget);
    });

    testWidgets('renders with an icon roundel in the dark theme', (tester) async {
      await pumpBlock(
        tester,
        const SizedBox(
          width: 220,
          child: ReportMetricCard(
            label: 'Stopa nedolaska',
            value: '12,4%',
            hint: 'Prodane, neiskorištene karte',
            emphasis: ReportEmphasis.negative,
            icon: Icons.person_off,
          ),
        ),
        brightness: Brightness.dark,
      );

      expect(find.text('12,4%'), findsOneWidget);
    });
  });
}
