import 'package:flutter/material.dart';

import 'core/api_client.dart';
import 'screens/home_screen.dart';
import 'screens/login_screen.dart';
import 'screens/register_screen.dart';
import 'services/auth_service.dart';

Future<void> main() async {
  WidgetsFlutterBinding.ensureInitialized();

  // The cookie jar backing every HTTP request must be ready before any
  // screen makes a call.
  await initApiClient();

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
    return MaterialApp(
      title: 'eKarta',
      theme: ThemeData(
        colorScheme: ColorScheme.fromSeed(seedColor: const Color(0xFF0D7C66)),
        useMaterial3: true,
      ),
      initialRoute: '/',
      routes: {
        '/': (_) => const HomeScreen(),
        '/login': (_) => const LoginScreen(),
        '/register': (_) => const RegisterScreen(),
      },
    );
  }
}
