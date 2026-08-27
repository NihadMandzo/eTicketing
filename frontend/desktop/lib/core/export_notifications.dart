import 'dart:async';

import 'package:flutter/foundation.dart';

import '../models/responses/ticket_print_batch_response.dart';
import '../providers/ticket_print_provider.dart';

/// App-wide awareness of ticket exports that are still going, ready to collect,
/// or failed — so an organizer can start a batch, walk away to another screen,
/// and still be told when the PDF is ready.
///
/// A plain [ValueNotifier] singleton rather than a state-management package:
/// this app deliberately has none (see .claude/rules/21-frontend-desktop.md),
/// and one notifier plus a timer is all this needs.
///
/// Polling rather than a push channel: the backend has no socket for the desktop
/// app to listen on, and a render finishing is not urgent to the second. The
/// interval steps down to [_activeInterval] only while something is actually
/// rendering, so an idle session costs one small request every half minute.
class ExportNotifications {
  static const _activeInterval = Duration(seconds: 5);
  static const _idleInterval = Duration(seconds: 30);

  final TicketPrintProvider _provider;

  ExportNotifications({TicketPrintProvider? provider}) : _provider = provider ?? TicketPrintProvider();

  /// Batches worth showing the organizer, newest first. Empty when there is
  /// nothing outstanding — which is also the state for platform staff, who have
  /// no organization of their own to export for.
  final ValueNotifier<List<TicketPrintBatchResponse>> batches = ValueNotifier([]);

  Timer? _timer;
  bool _isRunning = false;

  /// True when at least one batch has a file waiting to be downloaded — what
  /// turns the top-bar bell green.
  bool get hasReady => batches.value.any((b) => b.isDownloadable);

  bool get hasFailed => batches.value.any((b) => b.status == TicketPrintBatchStatus.failed);

  bool get hasInFlight => batches.value.any((b) => b.status.isInFlight);

  /// Begins polling. Safe to call more than once; call after a successful login.
  void start() {
    if (_isRunning) return;
    _isRunning = true;
    unawaited(refresh());
    _schedule(_idleInterval);
  }

  /// Stops polling and clears what was shown — call on logout, so one user's
  /// exports never linger in the next user's badge.
  void stop() {
    _isRunning = false;
    _timer?.cancel();
    _timer = null;
    batches.value = [];
  }

  /// Pulls the current list immediately. Called by the export screen after
  /// creating or downloading a batch, so the badge reacts without waiting for
  /// the next tick.
  Future<void> refresh() async {
    if (!_isRunning) return;

    // Cancel the pending tick before awaiting the request below, so a poll that takes longer than
    // the interval (slow network/backend) can never overlap with another one firing mid-flight.
    _timer?.cancel();

    try {
      final result = await _provider.getOutstanding();
      if (!_isRunning) return;
      batches.value = result;
    } catch (_) {
      // A failed poll is not worth interrupting anyone over — the badge simply
      // keeps showing what it last knew, and the next tick tries again. A truly
      // dead session is handled by the api client's own 401 interceptor.
    } finally {
      // Always re-schedule exactly once, success or failure, so polling never silently stops.
      // Watch closely while something is rendering, back off once it settles.
      _schedule(hasInFlight ? _activeInterval : _idleInterval);
    }
  }

  void _schedule(Duration interval) {
    _timer?.cancel();
    if (!_isRunning) return;
    _timer = Timer.periodic(interval, (_) => refresh());
  }
}

/// The single instance the shell and the export screen share.
final exportNotifications = ExportNotifications();
