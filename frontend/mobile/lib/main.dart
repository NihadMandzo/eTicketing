import 'package:flutter/material.dart';

import 'core/api_client.dart';
import 'screens/login_screen.dart';
import 'screens/main_shell.dart';
import 'services/auth_service.dart';
import 'theme/app_theme.dart';
import 'theme/theme_controller.dart';

Future<void> main() async {
  WidgetsFlutterBinding.ensureInitialized();

  // The cookie jar backing every HTTP request must be ready before any
  // screen makes a call.
  await initApiClient();

  // Restore the persisted light/dark preference before first paint.
  await ThemeController.init();

  // Attempt to silently restore a persisted session so a returning user
  // lands straight in the app shell instead of the login screen.
  var startSignedIn = false;
  try {
    startSignedIn = await AuthService().me() != null;
  } catch (_) {
    // No session, expired, or revoked — start signed out.
  }

  runApp(MyApp(startSignedIn: startSignedIn));
}

class MyApp extends StatelessWidget {
  final bool startSignedIn;

  const MyApp({super.key, this.startSignedIn = false});

  @override
  Widget build(BuildContext context) {
    return ValueListenableBuilder<ThemeMode>(
      valueListenable: ThemeController.mode,
      builder: (context, mode, _) {
        return MaterialApp(
          title: 'eKarta',
          theme: AppTheme.light,
          darkTheme: AppTheme.dark,
          themeMode: mode,
          // Login-gated app (see LoginScreen) — no guest-browsable landing
          // page. A restored session skips straight to the app shell.
          home: startSignedIn ? const MainShell() : const LoginScreen(),
        );
      },
    );
  }
}
