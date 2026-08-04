import 'package:flutter/material.dart';

import 'providers/api_exception.dart';
import 'screens/login_screen.dart';
import 'utility/snackbar_service.dart';

void main() {
  // Catch all Flutter framework errors (widget build errors, etc.)
  FlutterError.onError = (FlutterErrorDetails details) {
    FlutterError.presentError(details);
    SnackbarService.showError(details.exceptionAsString());
  };

  runApp(const MyApp());
}

class MyApp extends StatelessWidget {
  const MyApp({super.key});

  @override
  Widget build(BuildContext context) {
    return MaterialApp(
      title: 'eKarta Manager',
      navigatorKey: SnackbarService.navigatorKey,
      theme: ThemeData(
        colorScheme: ColorScheme.fromSeed(seedColor: const Color(0xFF0D7C66)),
        useMaterial3: true,
      ),
      home: const LoginScreen(),
    );
  }
}

// ---------------------------------------------------------------------------
// Convenience helpers — import from any screen or provider.
//
// Usage:
//
//   try {
//     final result = await _provider.insert(...);
//     handleApiSuccess('Event created successfully!');
//   } on ApiException catch (e) {
//     handleApiError(e);
//   } catch (e) {
//     handleApiError(e);
//   }
// ---------------------------------------------------------------------------

void handleApiSuccess(String message) {
  SnackbarService.showSuccess(message);
}

void handleApiError(Object error) {
  if (error is ApiException) {
    SnackbarService.showError(error.apiError.displayMessage);
  } else {
    SnackbarService.showError(error.toString());
  }
}

