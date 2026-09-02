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
}
