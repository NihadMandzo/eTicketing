import 'package:flutter/material.dart';

import 'core/api_client.dart';
import 'models/responses/user_profile.dart';
import 'providers/api_exception.dart';
import 'providers/auth_provider.dart';
import 'screens/login_screen.dart';
import 'screens/widgets/main_shell.dart';
import 'theme/app_theme.dart';
import 'theme/theme_controller.dart';
import 'utility/snackbar_service.dart';

Future<void> main() async {
  WidgetsFlutterBinding.ensureInitialized();

  // Catch all Flutter framework errors (widget build errors, etc.)
  FlutterError.onError = (FlutterErrorDetails details) {
    FlutterError.presentError(details);
    SnackbarService.showError(details.exceptionAsString());
  };

  // The cookie jar backing every HTTP request must be ready before any
  // screen (including the session-restore check below) makes a call.
  await initApiClient();

  // Wired once, at startup — api_client.dart deliberately has no screen
  // imports of its own, so it calls this to send the user back to the
  // login screen when a 401 survives a refresh attempt (refresh cookie
  // itself missing/expired/revoked, not just an expired access token).
  onSessionExpired = () {
    SnackbarService.showError('Sesija je istekla. Prijavite se ponovo.');
    SnackbarService.navigatorKey.currentState?.pushAndRemoveUntil(
      MaterialPageRoute(builder: (_) => const LoginScreen()),
      (_) => false,
    );
  };

  // Restore the persisted light/dark preference before first paint.
  await ThemeController.init();

  runApp(const MyApp());
}

class MyApp extends StatelessWidget {
  const MyApp({super.key});

  @override
  Widget build(BuildContext context) {
    return ValueListenableBuilder<ThemeMode>(
      valueListenable: ThemeController.mode,
      builder: (context, mode, _) {
        return MaterialApp(
          title: 'eKarta Manager',
          navigatorKey: SnackbarService.navigatorKey,
          theme: AppTheme.light,
          darkTheme: AppTheme.dark,
          themeMode: mode,
          home: const _SessionGate(),
        );
      },
    );
  }
}

/// Restores a persisted cookie-jar session across app restarts: if a valid
/// `eticketing_at`/`eticketing_rt` cookie is already on disk, `/auth/me`
/// succeeds and the user lands straight in [MainShell] instead of having to
/// log in again every time the app launches.
class _SessionGate extends StatefulWidget {
  const _SessionGate();

  @override
  State<_SessionGate> createState() => _SessionGateState();
}

class _SessionGateState extends State<_SessionGate> {
  static const _allowedRoles = {
    'SuperAdmin',
    'Admin',
    'OrganizationSuperAdmin',
    'OrganizationAdmin',
  };

  late final Future<UserProfile?> _restore = _tryRestoreSession();

  Future<UserProfile?> _tryRestoreSession() async {
    try {
      final profile = await AuthProvider().me();
      return _allowedRoles.contains(profile.roleName) ? profile : null;
    } catch (_) {
      // No session, expired, or revoked — fall through to the login screen.
      return null;
    }
  }

  @override
  Widget build(BuildContext context) {
    return FutureBuilder<UserProfile?>(
      future: _restore,
      builder: (context, snapshot) {
        if (snapshot.connectionState != ConnectionState.done) {
          return Scaffold(
            body: Center(
              child: CircularProgressIndicator(color: Theme.of(context).colorScheme.primary),
            ),
          );
        }
        final profile = snapshot.data;
        return profile != null ? MainShell(user: profile) : const LoginScreen();
      },
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
