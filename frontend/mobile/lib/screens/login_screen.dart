import 'package:flutter/material.dart';

import '../models/requests/login_request.dart';
import '../services/api_exception.dart';
import '../services/auth_service.dart';
import '../theme/app_colors.dart';
import '../widgets/labeled_field.dart';
import '../widgets/responsive_page.dart';
import 'forgot_password_screen.dart';
import 'register_screen.dart';

class LoginScreen extends StatefulWidget {
  const LoginScreen({super.key});

  @override
  State<LoginScreen> createState() => _LoginScreenState();
}

class _LoginScreenState extends State<LoginScreen> {
  final _formKey = GlobalKey<FormState>();
  final _emailOrUsernameCtrl = TextEditingController();
  final _passwordCtrl = TextEditingController();
  bool _isLoading = false;

  @override
  void dispose() {
    _emailOrUsernameCtrl.dispose();
    _passwordCtrl.dispose();
    super.dispose();
  }

  Future<void> _submit() async {
    if (!_formKey.currentState!.validate()) return;
    setState(() => _isLoading = true);

    try {
      await AuthService().login(LoginRequest(
        emailOrUsername: _emailOrUsernameCtrl.text.trim(),
        password: _passwordCtrl.text,
      ));

      if (!mounted) return;
      Navigator.of(context).pop();
    } on ApiException catch (e) {
      if (mounted) _showError(e.apiError.displayMessage);
    } catch (_) {
      if (mounted) _showError('Prijava nije uspjela. Pokušajte ponovo.');
    } finally {
      if (mounted) setState(() => _isLoading = false);
    }
  }

  void _showError(String message) {
    ScaffoldMessenger.of(context).showSnackBar(
      SnackBar(content: Text(message), backgroundColor: AppColors.errorDark),
    );
  }

  @override
  Widget build(BuildContext context) {
    final scheme = Theme.of(context).colorScheme;
    final isDark = Theme.of(context).brightness == Brightness.dark;
    final tertiaryText = isDark ? AppColors.darkTextTertiary : AppColors.lightTextTertiary;

    return Scaffold(
      appBar: AppBar(title: const Text('Prijava')),
      body: SafeArea(
        child: SingleChildScrollView(
          child: ResponsivePage(
            child: Form(
              key: _formKey,
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.stretch,
                children: [
                  const SizedBox(height: 8),
                  Center(
                    child: Column(
                      children: [
                        Icon(Icons.confirmation_number_rounded, size: 40, color: scheme.primary),
                        const SizedBox(height: 8),
                        Text(
                          'eKarta',
                          style: TextStyle(
                            fontSize: 22,
                            fontWeight: FontWeight.w700,
                            color: isDark ? AppColors.darkTextPrimary : AppColors.lightTextPrimary,
                          ),
                        ),
                      ],
                    ),
                  ),
                  const SizedBox(height: 28),
                  Text(
                    'Dobrodošli nazad',
                    style: TextStyle(
                      fontSize: 22,
                      fontWeight: FontWeight.w700,
                      color: isDark ? AppColors.darkTextPrimary : AppColors.lightTextPrimary,
                    ),
                  ),
                  const SizedBox(height: 6),
                  Text('Prijavite se na svoj eKarta nalog', style: TextStyle(fontSize: 14, color: tertiaryText)),
                  const SizedBox(height: 24),
                  LabeledField(
                    label: 'Email ili korisničko ime',
                    controller: _emailOrUsernameCtrl,
                    keyboardType: TextInputType.emailAddress,
                    hintText: 'amina@example.com',
                    prefixIcon: const Icon(Icons.person_outline_rounded),
                    validator: (v) => (v == null || v.trim().isEmpty) ? 'Unesite email ili korisničko ime' : null,
                  ),
                  const SizedBox(height: 16),
                  LabeledPasswordField(
                    label: 'Lozinka',
                    controller: _passwordCtrl,
                    hintText: '••••••••',
                    validator: (v) => (v == null || v.isEmpty) ? 'Unesite lozinku' : null,
                  ),
                  Align(
                    alignment: Alignment.centerRight,
                    child: TextButton(
                      onPressed: () => Navigator.of(context).push(
                        MaterialPageRoute(builder: (_) => const ForgotPasswordScreen()),
                      ),
                      child: const Text('Zaboravili ste lozinku?'),
                    ),
                  ),
                  const SizedBox(height: 8),
                  FilledButton(
                    style: FilledButton.styleFrom(padding: const EdgeInsets.symmetric(vertical: 15)),
                    onPressed: _isLoading ? null : _submit,
                    child: _isLoading
                        ? SizedBox(
                            width: 20,
                            height: 20,
                            child: CircularProgressIndicator(
                              strokeWidth: 2,
                              color: Theme.of(context).colorScheme.onPrimary,
                            ),
                          )
                        : const Text('Prijavite se'),
                  ),
                  const SizedBox(height: 24),
                  Wrap(
                    alignment: WrapAlignment.center,
                    children: [
                      Text('Nemate nalog? ', style: TextStyle(fontSize: 13, color: tertiaryText)),
                      GestureDetector(
                        onTap: () => Navigator.of(context).pushReplacement(
                          MaterialPageRoute(builder: (_) => const RegisterScreen()),
                        ),
                        child: Text(
                          'Registrujte se',
                          style: TextStyle(fontSize: 13, fontWeight: FontWeight.w700, color: scheme.primary),
                        ),
                      ),
                    ],
                  ),
                ],
              ),
            ),
          ),
        ),
      ),
    );
  }
}
