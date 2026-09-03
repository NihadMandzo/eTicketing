import 'package:desktop/screens/widgets/reports/report_bar_chart.dart';
import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';

/// The forecast/sales column chart.
///
/// Both rules pinned here came from the same screenshot: a month whose one
/// sold-out night dwarfed every other day, drawn beside a flat projection. The
/// small days rendered as nothing at all — the series looked like it stopped and
/// restarted — and the projection printed the same figure thirteen times over.
void main() {
  /// Finds the drawn columns. Every bar's body is the one `Container` in the
  /// tree constrained to the bar's 34px maximum width.
  Finder barBodies() => find.byWidgetPredicate(
        (widget) => widget is Container && widget.constraints?.maxWidth == 34,
      );

  Widget host(Widget child, {double width = 900}) => MaterialApp(
        home: Scaffold(body: Center(child: SizedBox(width: width, child: child))),
      );

  List<ReportBarData> series({
    required int actual,
    required int projected,
    double Function(int index)? ratio,
  }) =>
      [
        for (var i = 0; i < actual; i++)
          ReportBarData(label: 'd$i', value: '${i}k', ratio: ratio?.call(i) ?? 1.0),
        for (var i = 0; i < projected; i++)
          ReportBarData(label: 'p$i', value: '9k', ratio: 0.5, isProjected: true),
      ];

  group('bar heights', () {
    /// The bug as reported: a real but tiny amount beside a huge one drew
    /// nothing, so the chart appeared to break in the middle.
    testWidgets('draws a visible stub for a value far below the peak', (tester) async {
      await tester.pumpWidget(host(ReportBarChart(
        bars: [
          const ReportBarData(label: 'peak', value: '1.566k', ratio: 1),
          const ReportBarData(label: 'tiny', value: '35', ratio: 0.00002),
          const ReportBarData(label: 'zero', value: '', ratio: 0),
        ],
      )));

      final heights = tester.widgetList<Container>(barBodies());
      expect(heights, hasLength(3));

      final sizes = barBodies().evaluate().map((e) => tester.getSize(find.byWidget(e.widget)));
      expect(sizes.elementAt(1).height, greaterThan(2));
    });

    /// A day that sold nothing must stay flat. Floored to the same stub as a
    /// small sale, the chart would claim sales on a day that had none.
    testWidgets('leaves a zero bar flat', (tester) async {
      await tester.pumpWidget(host(ReportBarChart(
        bars: const [
          ReportBarData(label: 'peak', value: '1.566k', ratio: 1),
          ReportBarData(label: 'tiny', value: '35', ratio: 0.00002),
          ReportBarData(label: 'zero', value: '', ratio: 0),
        ],
      )));

      final bars = barBodies().evaluate().toList();
      final tiny = tester.getSize(find.byWidget(bars[1].widget)).height;
      final zero = tester.getSize(find.byWidget(bars[2].widget)).height;

      expect(zero, lessThan(tiny));
    });
  });

  group('value labels', () {
    testWidgets('labels every bar while the chart is short', (tester) async {
      await tester.pumpWidget(host(ReportBarChart(bars: series(actual: 5, projected: 5))));

      expect(find.text('0k'), findsOneWidget);
      expect(find.text('4k'), findsOneWidget);
      // One label per projected bar, all carrying the same figure.
      expect(find.text('9k'), findsNWidgets(5));
    });

    /// The screenshot's "559k" thirteen times in a row. Past a dozen bars only
    /// the tallest of each half keeps its figure.
    testWidgets('labels only the peak of each half once the chart is dense', (tester) async {
      await tester.pumpWidget(host(ReportBarChart(
        bars: series(actual: 6, projected: 13, ratio: (i) => i == 2 ? 1.0 : 0.1),
      )));

      // The actual peak, and the first bar of the flat projection — not all
      // thirteen of them.
      expect(find.text('2k'), findsOneWidget);
      expect(find.text('9k'), findsOneWidget);
      expect(find.text('0k'), findsNothing);
    });

    /// Suppressing a figure must not let its bar grow into the space: every bar
    /// keeps the same line of room above it, labelled or not.
    testWidgets('keeps every bar on the same baseline when labels are dropped', (tester) async {
      await tester.pumpWidget(host(ReportBarChart(
        bars: series(actual: 6, projected: 13, ratio: (i) => 0.5),
      )));

      final tops = barBodies()
          .evaluate()
          .map((e) => tester.getTopLeft(find.byWidget(e.widget)).dy)
          .toSet();

      expect(tops, hasLength(1));
    });
  });

  testWidgets('renders an empty series as a message rather than an empty frame', (tester) async {
    await tester.pumpWidget(host(const ReportBarChart(bars: [])));

    expect(find.text('Nema podataka za odabrani period.'), findsOneWidget);
  });

  testWidgets('scrolls rather than squeezing when the bars outgrow the box', (tester) async {
    await tester.pumpWidget(host(
      ReportBarChart(bars: series(actual: 6, projected: 13)),
      width: 300,
    ));

    expect(tester.takeException(), isNull);
    expect(find.byType(SingleChildScrollView), findsOneWidget);
  });
}
