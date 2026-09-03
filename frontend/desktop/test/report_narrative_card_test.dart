import 'package:desktop/screens/widgets/reports/report_narrative_card.dart';
import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';

/// The AI Uvidi executive summary.
///
/// The card exists to keep one long generated paragraph readable on a window
/// that can be 1900px wide, so the tests that matter are about the split: it
/// must add columns only when each still measures well, and it must never lose
/// a word of the summary while doing it.
void main() {
  /// A realistic summary — one Bosnian paragraph of several sentences, the
  /// shape `OpenAiCompatibleNarrativeWriter` returns.
  const narrative =
      'U periodu od 5. avgusta do 3. septembra 2026. ostvaren je ukupan prihod od 1.565.690,00 KM '
      'i prodano je 28.232 karte. Prosječna cijena karte iznosila je 55,46 KM, a otkazano je 0 '
      'karata. Većina prodaje realizovana je putem šaltera. Prema prognozi za narednih 14 dana '
      'očekuje se prosječna cijena od 55,88 KM. Identifikovano je pet neuobičajenih dana. '
      'Kupci visoke vrijednosti čine 25% svih kupaca, ali generišu 51,1% ukupnog prihoda.';

  /// Pumps the card into a window [width] wide.
  ///
  /// The surface size is set explicitly, not just the child's: a widget test's
  /// window is 800×600 by default, and a `SizedBox` cannot be wider than the
  /// constraints handed to it — so a card asked to lay out at 1800px would
  /// silently get 800 and every column assertion would pass for the wrong
  /// reason.
  Future<void> pumpCard(
    WidgetTester tester, {
    double width = 1800,
    Brightness brightness = Brightness.light,
  }) async {
    await tester.binding.setSurfaceSize(Size(width, 1200));
    addTearDown(() => tester.binding.setSurfaceSize(null));

    await tester.pumpWidget(MaterialApp(
      theme: ThemeData(brightness: brightness),
      home: const Scaffold(
        body: SingleChildScrollView(child: ReportNarrativeCard(narrative: narrative)),
      ),
    ));
  }

  group('ReportNarrativeCard.columnsFor', () {
    test('keeps one column on a narrow window', () {
      expect(ReportNarrativeCard.columnsFor(600), 1);
      expect(ReportNarrativeCard.columnsFor(900), 1);
    });

    test('splits once there is room for two full measures', () {
      expect(ReportNarrativeCard.columnsFor(1100), 2);
    });

    /// Three is the ceiling — a fourth column would still measure well at
    /// 1920px but reads as a layout exercise rather than a paragraph.
    test('never goes past three columns', () {
      expect(ReportNarrativeCard.columnsFor(1800), 3);
      expect(ReportNarrativeCard.columnsFor(4000), 3);
    });
  });

  group('ReportNarrativeCard.balanceIntoColumns', () {
    /// The one thing that must never happen: a summary quietly truncated so it
    /// fits a column count.
    test('keeps every word of the summary', () {
      for (final columns in [1, 2, 3]) {
        final joined = ReportNarrativeCard.balanceIntoColumns(narrative, columns).join(' ');
        expect(
          joined.replaceAll(RegExp(r'\s+'), ' ').trim(),
          narrative.replaceAll(RegExp(r'\s+'), ' ').trim(),
          reason: '$columns columns',
        );
      }
    });

    test('produces the requested number of non-empty columns', () {
      for (final columns in [2, 3]) {
        final parts = ReportNarrativeCard.balanceIntoColumns(narrative, columns);
        expect(parts, hasLength(columns), reason: '$columns columns');
        expect(parts.every((p) => p.trim().isNotEmpty), isTrue, reason: '$columns columns');
      }
    });

    test('breaks only between sentences', () {
      for (final part in ReportNarrativeCard.balanceIntoColumns(narrative, 3).skip(1)) {
        // A column that began mid-clause would start lower-case; every sentence
        // in the summary starts with a capital or a digit.
        expect(RegExp(r'^[A-ZČĆŽŠĐ0-9]').hasMatch(part), isTrue, reason: part);
      }
    });

    /// A one-sentence summary cannot be split three ways without cutting a
    /// clause in half, so it comes back whole and the card runs it full width.
    test('returns the text whole when it has fewer sentences than columns', () {
      const short = 'Prihod je stabilan.';

      expect(ReportNarrativeCard.balanceIntoColumns(short, 3), [short]);
    });

    /// A long opening sentence must not swallow the text and leave the last
    /// column empty — the guard that closes a column early when only just
    /// enough sentences remain.
    test('gives every column a sentence when the first one is long', () {
      final lopsided = '${'Vrlo duga uvodna rečenica o prihodu. ' * 5}Kratka. Još kraća.';
      final parts = ReportNarrativeCard.balanceIntoColumns(lopsided, 3);

      expect(parts, hasLength(3));
      expect(parts.every((p) => p.trim().isNotEmpty), isTrue);
    });
  });

  group('ReportNarrativeCard', () {
    testWidgets('sets the summary in columns on a wide window', (tester) async {
      await pumpCard(tester, width: 1800);

      expect(tester.takeException(), isNull);
      expect(find.text('AI SAŽETAK'), findsOneWidget);
      // One Text per column, plus the eyebrow above them.
      expect(find.byType(Text), findsNWidgets(ReportNarrativeCard.columnsFor(1800) + 1));
      // Columns, not stacked paragraphs: they share a top edge.
      final tops = tester
          .widgetList<Text>(find.byType(Text))
          .skip(1)
          .map((text) => tester.getTopLeft(find.text(text.data!)).dy)
          .toSet();
      expect(tops, hasLength(1));
    });

    testWidgets('renders as a single block on a narrow window', (tester) async {
      await pumpCard(tester, width: 700);

      expect(tester.takeException(), isNull);
      expect(find.text(narrative), findsOneWidget);
    });

    /// The desktop app resizes down to ~700px and this sits in the page's full
    /// width (.claude/rules/21-frontend-desktop.md).
    testWidgets('does not overflow at a narrow width', (tester) async {
      await pumpCard(tester, width: 400);

      expect(tester.takeException(), isNull);
    });

    testWidgets('paints in dark mode', (tester) async {
      await pumpCard(tester, brightness: Brightness.dark);

      expect(tester.takeException(), isNull);
    });
  });
}
