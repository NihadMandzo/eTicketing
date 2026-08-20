import 'package:flutter/material.dart';

import '../theme/app_colors.dart';
import '../widgets/legal_content_widgets.dart';
import '../widgets/responsive_page.dart';

/// "Politika privatnosti" — content ported verbatim from `frontend/web`'s
/// `pages/privacy/privacy.component.html`, for feature parity between the
/// two apps (web had this page, mobile didn't).
class PrivacyScreen extends StatelessWidget {
  const PrivacyScreen({super.key});

  @override
  Widget build(BuildContext context) {
    final isDark = Theme.of(context).brightness == Brightness.dark;
    final tertiaryText = isDark ? AppColors.darkTextTertiary : AppColors.lightTextTertiary;
    final bodyColor = isDark ? AppColors.darkTextSecondary : AppColors.lightTextSecondary;
    return Scaffold(
      appBar: AppBar(title: const Text('Politika privatnosti')),
      body: SafeArea(
        child: SingleChildScrollView(
          child: ResponsivePage(
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Text('Posljednje ažurirano: 4. Februar 2026.', style: TextStyle(fontSize: 12, color: tertiaryText)),
                const SizedBox(height: 12),
                Text(
                  'U eKarta platformi, poštovanje vaše privatnosti je naš prioritet. Ova Politika privatnosti objašnjava kako prikupljamo, koristimo, čuvamo i štitimo vaše lične podatke kada koristite naše usluge.',
                  style: TextStyle(fontSize: 14, height: 1.5, color: bodyColor),
                ),
                const SizedBox(height: 16),
                const LegalSection(
                  title: '1. Podaci koje prikupljamo',
                  paragraphs: ['Prikupljamo sljedeće vrste informacija:'],
                  bullets: [
                    'Lični podaci: ime i prezime, email adresa, broj telefona, adresa za dostavu (ako je potrebno), informacije o plaćanju (obrađene preko sigurnih procesora plaćanja)',
                    'Tehnički podaci: IP adresa, tip uređaja i operativni sistem, vrijeme pristupa i ekrani koje posjećujete',
                  ],
                ),
                const LegalSection(
                  title: '2. Kako koristimo vaše podatke',
                  paragraphs: ['Vaše podatke koristimo u sljedeće svrhe:'],
                  bullets: [
                    'Procesiranje i izvršavanje kupovina ulaznica',
                    'Slanje potvrda i ulaznica',
                    'Pružanje korisničke podrške',
                    'Poboljšanje naših usluga i korisničkog iskustva',
                    'Slanje važnih obavještenja vezanih za vašu kupovinu',
                    'Prevencija prevare i zaštita sigurnosti platforme',
                    'Ispunjavanje zakonskih obaveza',
                  ],
                ),
                const LegalSection(
                  title: '3. Dijeljenje podataka',
                  paragraphs: ['Vaše lične podatke možemo dijeliti u sljedećim okolnostima:'],
                  bullets: [
                    'Sa organizatorima događaja: radi validacije ulaznica',
                    'Sa procesorima plaćanja: u skladu sa PCI-DSS standardima',
                    'Sa pružaocima usluga: partneri koji nam pomažu u poslovanju (hosting, analitika, email servisi)',
                    'Po zakonskoj obavezi: kada je potrebno u skladu sa zakonom ili sudskim nalogom',
                  ],
                ),
                const LegalSection(
                  title: '4. Čuvanje podataka',
                  paragraphs: ['Vaše podatke čuvamo onoliko dugo koliko je potrebno da ispunimo svrhu za koju su prikupljeni, ili kako zahtijevaju relevantni zakoni. Općenito:'],
                  bullets: [
                    'Podaci o kupovinama se čuvaju najmanje 5 godina radi računovodstvenih zahtjeva',
                    'Korisnički računi ostaju aktivni sve dok ne zatražite brisanje',
                    'Tehnički logovi se čuvaju do 12 mjeseci',
                  ],
                ),
                const LegalSection(
                  title: '5. Sigurnost podataka',
                  paragraphs: [
                    'Ozbiljno shvatamo sigurnost vaših podataka i koristimo industrijske standarde zaštite:',
                  ],
                  bullets: [
                    'SSL/TLS enkripcija za sve komunikacije',
                    'Sigurno čuvanje podataka na enkriptovanim serverima',
                    'Redovne sigurnosne revizije i testiranja',
                    'Ograničen pristup ličnim podacima samo ovlašćenom osoblju',
                    'Dvostruka autentifikacija za administrativni pristup',
                  ],
                ),
                const LegalSection(
                  title: '6. Vaša prava',
                  paragraphs: ['U skladu sa zakonima o zaštiti podataka, imate sljedeća prava:'],
                  bullets: [
                    'Pristup: možete zatražiti kopiju vaših ličnih podataka',
                    'Ispravka: možete ažurirati netačne ili nepotpune podatke',
                    'Brisanje: možete zatražiti brisanje vaših podataka',
                    'Ograničenje: možete ograničiti kako koristimo vaše podatke',
                    'Prenosivost: možete zatražiti podatke u strukturiranom formatu',
                    'Prigovor: možete prigovoriti na određene načine korištenja vaših podataka',
                  ],
                ),
                const LegalSection(
                  title: '7. Djeca i privatnost',
                  paragraphs: [
                    'Naša platforma nije namijenjena djeci mlađoj od 16 godina. Svjesno ne prikupljamo lične podatke od djece. Ako saznamo da smo prikupili podatke djeteta mlađeg od 16 godina, odmah ćemo ih izbrisati.',
                  ],
                ),
                const LegalSection(
                  title: '8. Kontakt',
                  paragraphs: [
                    'Ako imate bilo kakvih pitanja ili zabrinutosti o ovoj Politici privatnosti ili obradi vaših podataka, možete nas kontaktirati:',
                    'Email: info@ekarta.ba\nTelefon: +387 33 123 456\nAdresa: Zmaja od Bosne 8, 71000 Sarajevo, BiH',
                  ],
                ),
              ],
            ),
          ),
        ),
      ),
    );
  }
}
