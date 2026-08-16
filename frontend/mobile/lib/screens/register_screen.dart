import 'package:flutter/material.dart';

import '../models/requests/register_request.dart';
import '../services/api_exception.dart';
import '../services/auth_service.dart';
import '../theme/app_colors.dart';
import '../widgets/labeled_field.dart';
import '../widgets/responsive_page.dart';
import 'login_screen.dart';
import 'verify_email_screen.dart';

/// Self-registration — always creates a "User"/buyer account on the backend
/// (the desktop admin console has no equivalent screen by design; admins and
/// organizers are provisioned separately by a SuperAdmin).
class RegisterScreen extends StatefulWidget {
  const RegisterScreen({super.key});

  @override
  State<RegisterScreen> createState() => _RegisterScreenState();
}

class _RegisterScreenState extends State<RegisterScreen> {
  final _formKey = GlobalKey<FormState>();
  final _firstNameCtrl = TextEditingController();
  final _lastNameCtrl = TextEditingController();
  final _emailCtrl = TextEditingController();
  final _usernameCtrl = TextEditingController();
  final _passwordCtrl = TextEditingController();
  final _confirmPasswordCtrl = TextEditingController();
  final _phoneCtrl = TextEditingController();
  bool _isLoading = false;

  @override
  void dispose() {
    _firstNameCtrl.dispose();
    _lastNameCtrl.dispose();
    _emailCtrl.dispose();
    _usernameCtrl.dispose();
    _passwordCtrl.dispose();
    _confirmPasswordCtrl.dispose();
    _phoneCtrl.dispose();
    super.dispose();
  }

  Future<void> _submit() async {
    if (!_formKey.currentState!.validate()) return;
    setState(() => _isLoading = true);

    try {
      await AuthService().register(RegisterRequest(
        firstName: _firstNameCtrl.text.trim(),
        lastName: _lastNameCtrl.text.trim(),
        email: _emailCtrl.text.trim(),
        username: _usernameCtrl.text.trim(),
        password: _passwordCtrl.text,
        phoneNumber: _phoneCtrl.text.trim().isEmpty ? null : _phoneCtrl.text.trim(),
      ));

      if (!mounted) return;
      Navigator.of(context).pushReplacement(
        MaterialPageRoute(builder: (_) => const VerifyEmailScreen()),
      );
    } on ApiException catch (e) {
      if (mounted) _showError(e.apiError.displayMessage);
    } catch (_) {
      if (mounted) _showError('Registracija nije uspjela. Pokušajte ponovo.');
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
      appBar: AppBar(title: const Text('Registracija')),
      body: SafeArea(
        child: SingleChildScrollView(
          child: ResponsivePage(
            child: Form(
              key: _formKey,
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.stretch,
                children: [
                  const SizedBox(height: 8),
                  Text(
                    'Kreirajte nalog',
                    style: TextStyle(
                      fontSize: 22,
                      fontWeight: FontWeight.w700,
                      color: isDark ? AppColors.darkTextPrimary : AppColors.lightTextPrimary,
                    ),
                  ),
                  const SizedBox(height: 6),
                  Text('Registrujte se da biste kupovali karte', style: TextStyle(fontSize: 14, color: tertiaryText)),
                  const SizedBox(height: 24),
                  Row(
                    crossAxisAlignment: CrossAxisAlignment.start,
                    children: [
                      Expanded(
                        child: LabeledField(
                          label: 'Ime',
                          controller: _firstNameCtrl,
                          validator: (v) => (v == null || v.trim().length < 2) ? 'Obavezno' : null,
                        ),
                      ),
                      const SizedBox(width: 12),
                      Expanded(
                        child: LabeledField(
                          label: 'Prezime',
                          controller: _lastNameCtrl,
                          validator: (v) => (v == null || v.trim().length < 2) ? 'Obavezno' : null,
                        ),
                      ),
                    ],
                  ),
                  const SizedBox(height: 16),
                  LabeledField(
                    label: 'Email',
                    controller: _emailCtrl,
                    keyboardType: TextInputType.emailAddress,
                    prefixIcon: const Icon(Icons.mail_outline_rounded),
                    validator: (v) {
                      if (v == null || v.trim().isEmpty) return 'Email je obavezan';
                      if (!RegExp(r'^[^@\s]+@[^@\s]+\.[^@\s]+$').hasMatch(v.trim())) return 'Neispravan email';
                      return null;
                    },
                  ),
                  const SizedBox(height: 16),
                  LabeledField(
                    label: 'Korisničko ime',
                    controller: _usernameCtrl,
                    prefixIcon: const Icon(Icons.alternate_email_rounded),
                    validator: (v) => (v == null || v.trim().length < 3) ? 'Minimalno 3 karaktera' : null,
                  ),
                  const SizedBox(height: 16),
                  LabeledField(
                    label: 'Broj telefona (opciono)',
                    controller: _phoneCtrl,
                    keyboardType: TextInputType.phone,
                    prefixIcon: const Icon(Icons.phone_outlined),
                    validator: (v) {
                      if (v == null || v.trim().isEmpty) return null;
                      return RegExp(r'^\+?[0-9\s\-()]{6,20}$').hasMatch(v.trim())
                          ? null
                          : 'Broj telefona nije u ispravnom formatu';
                    },
                  ),
                  const SizedBox(height: 16),
                  LabeledPasswordField(
                    label: 'Lozinka',
                    controller: _passwordCtrl,
                    hintText: '••••••••',
                    validator: (v) => (v == null || v.length < 8) ? 'Minimalno 8 karaktera' : null,
                  ),
                  const SizedBox(height: 16),
                  LabeledPasswordField(
                    label: 'Potvrdite lozinku',
                    controller: _confirmPasswordCtrl,
                    hintText: '••••••••',
                    validator: (v) => (v != _passwordCtrl.text) ? 'Lozinke se ne podudaraju' : null,
                  ),
                  const SizedBox(height: 24),
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
                        : const Text('Registrujte se'),
                  ),
                  const SizedBox(height: 20),
                  Wrap(
                    alignment: WrapAlignment.center,
                    children: [
                      Text('Već imate nalog? ', style: TextStyle(fontSize: 13, color: tertiaryText)),
                      GestureDetector(
                        onTap: () => Navigator.of(context).pushReplacement(
                          MaterialPageRoute(builder: (_) => const LoginScreen()),
                        ),
                        child: Text(
                          'Prijavite se',
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
