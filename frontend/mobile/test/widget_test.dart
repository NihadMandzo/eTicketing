import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:mobile/main.dart';
import 'package:mobile/screens/login_screen.dart';

void main() {
  // The app is login-gated (see MyApp.home) — a cold start with no restored
  // session must land on LoginScreen, not the shell.
  testWidgets('signed-out start renders the login screen', (WidgetTester tester) async {
    await tester.pumpWidget(const MyApp(startSignedIn: false));
    await tester.pump();

    expect(find.byType(LoginScreen), findsOneWidget);
    expect(find.byType(MaterialApp), findsOneWidget);
  });
}
