import 'package:desktop/models/responses/report_responses.dart';
import 'package:desktop/screens/widgets/reports/report_insight_card.dart';
import 'package:desktop/screens/widgets/reports/report_segment_card.dart';
import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';

/// The AI Uvidi cards, pumped for real.
///
/// These exist because the first build of `ReportInsightCard` rendered as blank
/// space on the running app: the card carried a non-uniform `Border` (a thick
/// coloured left edge, thin neutral elsewhere) together with a `borderRadius`,
/// which Flutter does not allow. `flutter analyze` cannot see that — it is a
/// runtime assertion in the painting layer — so nothing caught it until it was
/// on screen.
void main() {
  BusinessInsight insight({
    InsightSeverity severity = InsightSeverity.warning,
    InsightCategory category = InsightCategory.forecast,
    String? metric = '12,4%',
  }) =>
      BusinessInsight(
        severity: severity,
        category: category,
        title: 'Prodaja opada u narednih 14 dana',
        body: 'Projekcija prihoda je 1.284,00 KM, −18,2% u odnosu na prethodnih 14 dana.',
        metric: metric,
      );

  /// A realistic host: a bounded, scrollable column, which is what both the
  /// Izvještaji tab and the Dashboard put these cards in.
  Widget host(Widget child, {double width = 900, Brightness brightness = Brightness.light}) => MaterialApp(
        theme: ThemeData(brightness: brightness),
        home: Scaffold(
          body: SingleChildScrollView(
            child: SizedBox(width: width, child: child),
          ),
        ),
      );

  group('ReportInsightCard', () {
    testWidgets('renders its title, body and metric', (tester) async {
      await tester.pumpWidget(host(ReportInsightCard(insight: insight())));

      expect(tester.takeException(), isNull);
      expect(find.text('Prodaja opada u narednih 14 dana'), findsOneWidget);
      expect(find.textContaining('1.284,00 KM'), findsOneWidget);
      expect(find.text('12,4%'), findsOneWidget);
    });

    testWidgets('renders without a metric chip', (tester) async {
      await tester.pumpWidget(host(ReportInsightCard(insight: insight(metric: null))));

      expect(tester.takeException(), isNull);
      expect(find.text('12,4%'), findsNothing);
    });

    /// Every severity and category combination paints — the accent colour and
    /// the icon are both switched on them, and a missing arm would only show up
    /// on the one report that happened to produce it.
    testWidgets('paints for every severity', (tester) async {
      for (final severity in InsightSeverity.values) {
        await tester.pumpWidget(host(ReportInsightCard(insight: insight(severity: severity))));
        expect(tester.takeException(), isNull, reason: 'severity $severity');
      }
    });

    testWidgets('paints for every category', (tester) async {
      for (final category in InsightCategory.values) {
        await tester.pumpWidget(host(ReportInsightCard(insight: insight(category: category))));
        expect(tester.takeException(), isNull, reason: 'category $category');
      }
    });

    testWidgets('paints in dark mode', (tester) async {
      await tester.pumpWidget(host(ReportInsightCard(insight: insight()), brightness: Brightness.dark));

      expect(tester.takeException(), isNull);
    });

    /// The desktop app is resizable down to ~700px and these sit inside a
    /// two-column layout, so the card has to survive a genuinely narrow box
    /// without a RenderFlex overflow (.claude/rules/21-frontend-desktop.md).
    testWidgets('does not overflow at a narrow width', (tester) async {
      await tester.pumpWidget(host(ReportInsightCard(insight: insight()), width: 300));

      expect(tester.takeException(), isNull);
    });

    /// Stacked, which is how both callers use them — a layout fault that only
    /// appears with siblings would otherwise slip through.
    testWidgets('renders stacked in a column', (tester) async {
      await tester.pumpWidget(host(Column(
        children: [
          for (final severity in InsightSeverity.values) ReportInsightCard(insight: insight(severity: severity)),
        ],
      )));

      expect(tester.takeException(), isNull);
      expect(find.text('Prodaja opada u narednih 14 dana'), findsNWidgets(InsightSeverity.values.length));
    });
  });

  group('ReportSegmentCard', () {
    AudienceSegment segment({double share = 25, double revenueShare = 60}) => AudienceSegment(
          name: 'Kupci visoke vrijednosti',
          description: 'Prosječno 420,00 KM po kupcu — kupuju često, aktivni su u zadnjih mjesec dana.',
          buyers: 9,
          sharePercent: share,
          revenueSharePercent: revenueShare,
          averageSpend: 420,
          averageTickets: 4.5,
          averageRecencyDays: 12,
        );

    testWidgets('renders its name, description and both share bars', (tester) async {
      await tester.pumpWidget(host(ReportSegmentCard(segment: segment(), isLeading: true)));

      expect(tester.takeException(), isNull);
      expect(find.text('Kupci visoke vrijednosti'), findsOneWidget);
      expect(find.text('Udio kupaca'), findsOneWidget);
      expect(find.text('Udio prihoda'), findsOneWidget);
      expect(find.byType(LinearProgressIndicator), findsNWidgets(2));
    });

    /// Four real fields off `AudienceSegment`, not two — in whichever grid the
    /// card's width calls for.
    testWidgets('renders all four stats in its grid', (tester) async {
      await tester.pumpWidget(host(ReportSegmentCard(segment: segment())));

      expect(tester.takeException(), isNull);
      expect(find.text('Prosječna potrošnja'), findsOneWidget);
      expect(find.text('Zadnja kupovina'), findsOneWidget);
      expect(find.text('Broj kupaca'), findsOneWidget);
      expect(find.text('Prosječno karata'), findsOneWidget);
      // averageTickets: 4.5 in the fixture segment().
      expect(find.text('4,5'), findsOneWidget);
    });

    /// The wide arrangement, which is what a maximised window produces. Both
    /// share bars sit on one line and all four stats on the next, so the card
    /// gets denser instead of flinging each label and its figure to opposite
    /// edges of a 900px box.
    testWidgets('lays out bars and stats in rows once it is wide', (tester) async {
      await tester.pumpWidget(host(ReportSegmentCard(segment: segment()), width: 700));

      expect(tester.takeException(), isNull);

      final buyers = tester.getTopLeft(find.text('Udio kupaca'));
      final revenue = tester.getTopLeft(find.text('Udio prihoda'));
      expect(revenue.dy, buyers.dy);
      expect(revenue.dx, greaterThan(buyers.dx));

      final firstStat = tester.getTopLeft(find.text('Prosječna potrošnja'));
      final lastStat = tester.getTopLeft(find.text('Prosječno karata'));
      expect(lastStat.dy, firstStat.dy);
    });

    /// Below the breakpoint the same four figures stack — bars above each
    /// other, stats 2×2 — rather than being squeezed onto one line.
    testWidgets('stacks bars and pairs stats when narrow', (tester) async {
      await tester.pumpWidget(host(ReportSegmentCard(segment: segment()), width: 380));

      expect(tester.takeException(), isNull);

      final buyers = tester.getTopLeft(find.text('Udio kupaca'));
      final revenue = tester.getTopLeft(find.text('Udio prihoda'));
      expect(revenue.dy, greaterThan(buyers.dy));

      final firstStat = tester.getTopLeft(find.text('Prosječna potrošnja'));
      final lastStat = tester.getTopLeft(find.text('Prosječno karata'));
      expect(lastStat.dy, greaterThan(firstStat.dy));
    });

    /// A share is a percentage of a whole and cannot exceed it, but a rounding
    /// artefact must not throw off the progress track's layout.
    testWidgets('clamps an out-of-range share instead of throwing', (tester) async {
      await tester.pumpWidget(host(ReportSegmentCard(segment: segment(share: 140, revenueShare: -5))));

      expect(tester.takeException(), isNull);
    });

    testWidgets('does not overflow at a narrow width', (tester) async {
      await tester.pumpWidget(host(ReportSegmentCard(segment: segment()), width: 300));

      expect(tester.takeException(), isNull);
    });

    testWidgets('paints in dark mode', (tester) async {
      await tester.pumpWidget(host(ReportSegmentCard(segment: segment()), brightness: Brightness.dark));

      expect(tester.takeException(), isNull);
    });
  });

  /// The no-orphan rule for the Segmenti kupaca grid: K-Means asks for four
  /// clusters and drops empty ones, so three segments is routine, and laying
  /// three out two-across is what left half a row of white space beside the
  /// last card.
  group('ReportSegmentCard.columnsFor', () {
    test('never leaves a partly filled row', () {
      for (var count = 1; count <= 4; count++) {
        for (final width in [320.0, 700.0, 1060.0, 1400.0, 1810.0]) {
          final columns = ReportSegmentCard.columnsFor(count, width);

          expect(
            count % columns,
            0,
            reason: '$count segments at ${width}px came out $columns across',
          );
        }
      }
    });

    test('fills the row with one card each when they fit', () {
      expect(ReportSegmentCard.columnsFor(3, 1060), 3);
      expect(ReportSegmentCard.columnsFor(4, 1810), 4);
      expect(ReportSegmentCard.columnsFor(2, 700), 2);
    });

    /// Rather than squeezing four cards below the width they are readable at,
    /// the grid steps down to the next divisor — two rows of two.
    test('drops to a divisor rather than below the minimum card width', () {
      expect(ReportSegmentCard.columnsFor(4, 1060), 2);
      expect(ReportSegmentCard.columnsFor(3, 700), 1);
      expect(ReportSegmentCard.columnsFor(4, 500), 1);
    });

    test('keeps every card at or above the readable minimum', () {
      for (var count = 1; count <= 4; count++) {
        for (final width in [400.0, 700.0, 1060.0, 1810.0]) {
          final columns = ReportSegmentCard.columnsFor(count, width);
          if (columns == 1) continue;

          final cardWidth = (width - 16 * (columns - 1)) / columns;
          expect(cardWidth, greaterThanOrEqualTo(ReportSegmentCard.minWidth),
              reason: '$count segments at ${width}px');
        }
      }
    });

  });
}
