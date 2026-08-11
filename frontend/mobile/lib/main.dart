import 'package:flutter/material.dart';

import 'core/api_client.dart';
import 'screens/home_screen.dart';
import 'screens/login_screen.dart';
import 'screens/register_screen.dart';
import 'screens/settings_screen.dart';
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

  // Attempt to silently restore a persisted session (guest browsing works
  // either way — this only pre-fills Session.currentUser if one exists).
  try {
    await AuthService().me();
  } catch (_) {
    // No session, expired, or revoked — the app just starts in guest mode.
  }

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
          title: 'eKarta',
          theme: AppTheme.light,
          darkTheme: AppTheme.dark,
          themeMode: mode,
          initialRoute: '/',
          routes: {
            '/': (_) => const HomeScreen(),
            '/login': (_) => const LoginScreen(),
            '/register': (_) => const RegisterScreen(),
            '/settings': (_) => const SettingsScreen(),
          },
        );
      },
    );
  }
}
