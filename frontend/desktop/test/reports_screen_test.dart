import 'dart:async';

import 'package:desktop/core/formatting.dart';
import 'package:desktop/models/api_error.dart';
import 'package:desktop/models/responses/report_responses.dart';
import 'package:desktop/models/responses/user_profile.dart';
import 'package:desktop/providers/api_exception.dart';
import 'package:desktop/providers/report_provider.dart';
import 'package:desktop/screens/reports_screen.dart';
import 'package:desktop/screens/widgets/reports/report_horizon_bar.dart';
import 'package:desktop/screens/widgets/reports/report_range_bar.dart';
import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';

/// The Izvještaji screen's request handling.
///
/// The thing worth pinning is what happens when requests overlap: the user changes the projection
/// and, before it answers, the date range. The screen used to keep a flag per kind of request, each
/// cleared only by its own request's completion — which a superseded request skips on purpose — so
/// the projection pills stayed locked on a spinner for good. These tests drive that exact sequence
/// with a provider whose responses the test releases by hand.
void main() {
  group('ReportsScreen overlapping requests', () {
    testWidgets('a horizon change superseded by a date change does not lock the pills', (tester) async {
      final provider = await _openInsights(tester);
      await _answerLatest(tester, provider, revenue: 100); // first AI Uvidi load, horizon 30

      await _tapHorizon(tester, '6 mjeseci');
      expect(provider.insightsCalls.last.horizon, 180);
      expect(_busyPill(tester), isTrue, reason: 'the clicked pill shows its own spinner');

      // Before the 6-month answer arrives, the user picks another period.
      await tester.tap(_presetFinder('7 dana'));
      await tester.pump();
      expect(provider.insightsCalls, hasLength(3));

      await _answerLatest(tester, provider, revenue: 300);

      expect(_busyPill(tester), isFalse, reason: 'nothing is in flight any more');
      await _tapHorizon(tester, '1 godina');
      expect(provider.insightsCalls, hasLength(4), reason: 'the pill must accept clicks again');
      expect(provider.insightsCalls.last.horizon, 365);
    });

    testWidgets('a superseded response is dropped, even when it lands last', (tester) async {
      final provider = await _openInsights(tester);
      await _answerLatest(tester, provider, revenue: 100);

      await _tapHorizon(tester, '3 mjeseca');
      final stale = provider.insightsCalls.last;
      await tester.tap(_presetFinder('7 dana'));
      await tester.pump();
      final fresh = provider.insightsCalls.last;

      fresh.complete(_insights(horizon: fresh.horizon, revenue: 300));
      await tester.pumpAndSettle();
      stale.complete(_insights(horizon: stale.horizon, revenue: 999));
      await tester.pumpAndSettle();

      expect(find.text(formatMoney(300)), findsWidgets);
      expect(find.text(formatMoney(999)), findsNothing);
    });

    testWidgets('a tab switch during a horizon change does not lock the pills either', (tester) async {
      final provider = await _openInsights(tester);
      await _answerLatest(tester, provider, revenue: 100);

      await _tapHorizon(tester, '1 godina');
      await tester.tap(find.text('Prodaja'));
      await tester.pumpAndSettle();
      await tester.tap(find.text('AI Uvidi'));
      await tester.pump();
      await _answerLatest(tester, provider, revenue: 200);

      expect(_busyPill(tester), isFalse);
      await _tapHorizon(tester, '3 mjeseca');
      expect(provider.insightsCalls.last.horizon, 90);
    });

    testWidgets('a failed horizon change puts the selection back on the figures shown', (tester) async {
      final provider = await _openInsights(tester);
      await _answerLatest(tester, provider, revenue: 100);

      await _tapHorizon(tester, '6 mjeseci');
      provider.insightsCalls.last.fail(ApiException(statusCode: 500, apiError: ApiError(message: 'Greška')));
      await tester.pumpAndSettle();

      expect(_busyPill(tester), isFalse);
      expect(_selectedHorizonLabel(tester), '1 mjesec', reason: 'the figures are still the 30-day ones');
      await _tapHorizon(tester, '6 mjeseci');
      expect(provider.insightsCalls, hasLength(3), reason: 'retrying is possible');
    });
  });
}

// ── Harness ──────────────────────────────────────────────────────────────────────────────────

/// Opens the screen as an organization admin, lands on AI Uvidi and leaves its first request
/// pending, so each test decides when it is answered.
Future<_FakeReportProvider> _openInsights(WidgetTester tester) async {
  tester.view.physicalSize = const Size(1600, 2400);
  tester.view.devicePixelRatio = 1;
  addTearDown(tester.view.reset);

  final provider = _FakeReportProvider();
  await tester.pumpWidget(
    MaterialApp(home: Scaffold(body: ReportsScreen(user: _user, provider: provider))),
  );
  await tester.pumpAndSettle(); // the Prodaja tab answers immediately

  await tester.tap(find.text('AI Uvidi'));
  await tester.pump();
  expect(provider.insightsCalls, hasLength(1));
  return provider;
}

/// Answers the newest pending AI Uvidi request and lets the screen settle.
Future<void> _answerLatest(WidgetTester tester, _FakeReportProvider provider, {required double revenue}) async {
  final call = provider.insightsCalls.last;
  call.complete(_insights(horizon: call.horizon, revenue: revenue));
  await tester.pumpAndSettle();
}

Finder _horizonPill(String label) =>
    find.descendant(of: find.byType(ReportHorizonBar), matching: find.text(label));

// The date presets share two labels with the projection pills ("3 mjeseca", "6 mjeseci"), so
// anything looked up by text has to say which strip it means.
Finder _presetFinder(String label) =>
    find.descendant(of: find.byType(ReportRangeBar), matching: find.text(label));

Future<void> _tapHorizon(WidgetTester tester, String label) async {
  await tester.tap(_horizonPill(label));
  await tester.pump();
}

/// True while any projection pill shows its spinner.
bool _busyPill(WidgetTester tester) => find
    .descendant(of: find.byType(ReportHorizonBar), matching: find.byType(CircularProgressIndicator))
    .evaluate()
    .isNotEmpty;

String _selectedHorizonLabel(WidgetTester tester) {
  final bar = tester.widget<ReportHorizonBar>(find.byType(ReportHorizonBar));
  return ReportHorizon.all.firstWhere((h) => h.days == bar.selectedDays).label;
}

final _user = UserProfile(
  id: 'u1',
  firstName: 'Test',
  lastName: 'Org',
  username: 'org',
  email: 'org@example.ba',
  isActive: true,
  isEmailVerified: true,
  isFirstLogin: false,
  roleName: 'OrganizationSuperAdmin',
  createdAt: DateTime(2026, 1, 1),
);

const _period = {'from': '2026-09-07', 'to': '2026-10-07', 'days': 31, 'bucketUnit': 0};

AnalyticsInsights _insights({required int horizon, required double revenue}) =>
    AnalyticsInsights.fromJson({
      'period': _period,
      'scope': 'Test',
      'forecast': {
        'source': 0,
        'horizon': horizon,
        'projectedRevenue': revenue,
        'projectedSold': 1,
        'changePercent': null,
        'actual': <Object>[],
        'points': <Object>[],
      },
      'anomalies': {'source': 0, 'items': <Object>[]},
      'segments': {'source': 0, 'windowLabel': '', 'totalBuyers': 0, 'items': <Object>[]},
      'insights': <Object>[],
      'narrative': null,
    });

/// One AI Uvidi request the screen has made and the test has not answered yet.
class _InsightsCall {
  final int horizon;
  final Completer<AnalyticsInsights> _completer = Completer();

  _InsightsCall(this.horizon);

  void complete(AnalyticsInsights value) {
    if (!_completer.isCompleted) _completer.complete(value);
  }

  void fail(Object error) {
    if (!_completer.isCompleted) _completer.completeError(error);
  }
}

class _FakeReportProvider extends ReportProvider {
  final List<_InsightsCall> insightsCalls = [];

  @override
  Future<SalesReport> getSales(DateTime from, DateTime to) async => SalesReport.fromJson({
        'period': _period,
        'scope': 'Test',
      });

  @override
  Future<AnalyticsInsights> getInsights(DateTime from, DateTime to, {required int horizon}) {
    final call = _InsightsCall(horizon);
    insightsCalls.add(call);
    return call._completer.future;
  }
}
