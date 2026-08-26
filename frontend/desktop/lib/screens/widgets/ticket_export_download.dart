import 'dart:io';

import 'package:file_picker/file_picker.dart';
import 'package:flutter/material.dart';

import '../../core/export_notifications.dart';
import '../../main.dart';
import '../../models/responses/ticket_print_batch_response.dart';
import '../../providers/ticket_print_provider.dart';

/// Saves a finished export to disk, from wherever the organizer happens to be —
/// the export screen itself or the top-bar badge.
///
/// Shared because the server destroys its copy as it hands the bytes over: the
/// download is a one-shot, so both entry points must do exactly the same thing
/// with it, including refreshing the badge afterwards so the collected batch
/// stops being advertised.
///
/// Returns true when the file was written, false when the organizer cancelled
/// the save dialog or the download failed.
Future<bool> saveTicketExport(
  BuildContext context,
  TicketPrintBatchResponse batch, {
  TicketPrintProvider? provider,
}) async {
  final suggested = 'ulaznice-${batch.serialFrom.toString().padLeft(6, '0')}'
      '-${batch.serialTo.toString().padLeft(6, '0')}.pdf';

  final path = await FilePicker.platform.saveFile(
    dialogTitle: 'Sačuvaj PDF sa ulaznicama',
    fileName: suggested,
    type: FileType.custom,
    allowedExtensions: const ['pdf'],
  );
  if (path == null) return false;

  try {
    final bytes = await (provider ?? TicketPrintProvider()).downloadFile(batch.id);
    await File(path).writeAsBytes(bytes, flush: true);

    handleApiSuccess('PDF sa ${batch.ticketCount} ulaznica je sačuvan.');
    await exportNotifications.refresh();
    return true;
  } catch (e) {
    handleApiError(e);
    return false;
  }
}
