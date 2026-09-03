import 'package:desktop/screens/widgets/reports/report_horizon_bar.dart';
import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';

/// The AI Uvidi projection selector.
///
/// Two things here are worth pinning. The four values are the ones
/// `InsightsQueryValidator.AllowedHorizons` accepts — a pill offering anything
/// else would produce a 400 on click, not a longer forecast — and the Bosnian
/// wording is duplicated from the backend's `HorizonLabel`, so a drift between
/// the two would have the tile and the finding beside it name the same period
/// differently.
void main() {
  Widget host(Widget child, {double width = 1200, Brightness brightness = Brightness.light}) =>
      MaterialApp(
        theme: ThemeData(brightness: brightness),
        home: Scaffold(body: SingleChildScrollView(child: SizedBox(width: width, child: child))),
      );

  group('ReportHorizon', () {
    /// Mirrors InsightsQueryValidator.AllowedHorizons.
    test('offers exactly the horizons the API accepts', () {
      expect(ReportHorizon.all.map((h) => h.days), [30, 90, 180, 365]);
    });

    test('opens on the shortest horizon', () {
      expect(ReportHorizon.defaultDays, 30);
      expect(ReportHorizon.all.first.days, ReportHorizon.defaultDays);
    });

    /// Mirrors HorizonLabel.Phrase — same four wordings, so a tile written here
    /// and a finding written server-side name the same period the same way.
    test('words each horizon as months, not as a day count', () {
      expect(ReportHorizon.phraseFor(30), 'mjesec dana');
      expect(ReportHorizon.phraseFor(90), '3 mjeseca');
      expect(ReportHorizon.phraseFor(180), '6 mjeseci');
      expect(ReportHorizon.phraseFor(365), 'godinu dana');
    });

    test('builds the sentence fragments the tiles use', () {
      expect(ReportHorizon.next(365), 'narednih godinu dana');
      expect(ReportHorizon.previous(90), 'prethodnih 3 mjeseca');
    });

    /// A horizon this table does not know still has to read as Bosnian rather
    /// than crash the tile it is being written into.
    test('falls back to a day count for an unknown horizon', () {
      expect(ReportHorizon.phraseFor(45), '45 dana');
    });
  });

  group('ReportHorizonBar', () {
    testWidgets('renders one pill per horizon', (tester) async {
      await tester.pumpWidget(host(ReportHorizonBar(selectedDays: 30, onSelect: (_) {})));

      expect(tester.takeException(), isNull);
      expect(find.text('Projekcija:'), findsOneWidget);
      for (final horizon in ReportHorizon.all) {
        expect(find.text(horizon.label), findsOneWidget);
      }
    });

    testWidgets('reports the horizon that was tapped', (tester) async {
      ReportHorizon? picked;
      await tester.pumpWidget(
        host(ReportHorizonBar(selectedDays: 30, onSelect: (horizon) => picked = horizon)),
      );

      await tester.tap(find.text('6 mjeseci'));
      await tester.pump();

      expect(picked?.days, 180);
    });

    /// Re-picking the horizon already on screen would re-run the most expensive
    /// call the back-office makes for an identical answer.
    testWidgets('does not re-fire for the horizon already selected', (tester) async {
      var calls = 0;
      await tester.pumpWidget(
        host(ReportHorizonBar(selectedDays: 90, onSelect: (_) => calls++)),
      );

      await tester.tap(find.text('3 mjeseca'));
      await tester.pump();

      expect(calls, 0);
    });

    /// The spinner sits in the pill, not over the tab: the point of the quiet
    /// re-fetch is that the figures already on screen stay put.
    testWidgets('shows a spinner in the selected pill while refreshing', (tester) async {
      await tester.pumpWidget(host(
        ReportHorizonBar(selectedDays: 180, onSelect: (_) {}, isRefreshing: true),
      ));

      expect(find.byType(CircularProgressIndicator), findsOneWidget);
    });

    testWidgets('refuses input while a change is in flight', (tester) async {
      var calls = 0;
      await tester.pumpWidget(host(
        ReportHorizonBar(selectedDays: 30, onSelect: (_) => calls++, isRefreshing: true),
      ));

      await tester.tap(find.text('1 godina'));
      await tester.pump();

      expect(calls, 0);
    });

    testWidgets('refuses input while the tab itself is loading', (tester) async {
      await tester.pumpWidget(host(const ReportHorizonBar(selectedDays: 30, onSelect: null)));

      await tester.tap(find.text('3 mjeseca'));
      await tester.pump();

      expect(tester.takeException(), isNull);
    });

    /// The desktop app resizes down to ~700px (.claude/rules/21-frontend-desktop.md).
    testWidgets('does not overflow at a narrow width', (tester) async {
      await tester.pumpWidget(
        host(ReportHorizonBar(selectedDays: 30, onSelect: (_) {}), width: 420),
      );

      expect(tester.takeException(), isNull);
    });

    testWidgets('paints in dark mode', (tester) async {
      await tester.pumpWidget(host(
        ReportHorizonBar(selectedDays: 30, onSelect: (_) {}),
        brightness: Brightness.dark,
      ));

      expect(tester.takeException(), isNull);
    });
  });
}
