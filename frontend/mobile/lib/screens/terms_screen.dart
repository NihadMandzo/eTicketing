import 'package:flutter/material.dart';

import '../theme/app_colors.dart';
import '../widgets/legal_content_widgets.dart';
import '../widgets/responsive_page.dart';

/// "Uslovi korištenja" — content ported verbatim from `frontend/web`'s
/// `pages/terms/terms.component.html`, for feature parity between the two
/// apps (web had this page, mobile didn't).
class TermsScreen extends StatelessWidget {
  const TermsScreen({super.key});

  @override
  Widget build(BuildContext context) {
    final isDark = Theme.of(context).brightness == Brightness.dark;
    final tertiaryText = isDark ? AppColors.darkTextTertiary : AppColors.lightTextTertiary;
    return Scaffold(
      appBar: AppBar(title: const Text('Uslovi korištenja')),
      body: SafeArea(
        child: SingleChildScrollView(
          child: ResponsivePage(
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Text('Posljednje ažurirano: 4. Februar 2026.', style: TextStyle(fontSize: 12, color: tertiaryText)),
                const SizedBox(height: 16),
                const LegalSection(
                  title: '1. Prihvatanje uslova',
                  paragraphs: [
                    'Dobrodošli na eKarta platformu za prodaju ulaznica. Korištenjem naših usluga, prihvatate da budete vezani ovim Uslovima korištenja. Ako se ne slažete sa bilo kojim dijelom ovih uslova, molimo vas da ne koristite naše usluge.',
                  ],
                ),
                const LegalSection(
                  title: '2. Definicije',
                  paragraphs: [
                    '"Platforma" označava aplikaciju eKarta i sve povezane usluge.',
                    '"Korisnik" označava bilo koju osobu koja koristi našu platformu.',
                    '"Organizator" označava osobu ili entitet koji prodaje ulaznice putem naše platforme.',
                    '"Događaj" označava bilo koji koncert, predstavu, sportski događaj ili drugu aktivnost za koju se prodaju ulaznice.',
                  ],
                ),
                const LegalSection(
                  title: '3. Registracija i korisnički račun',
                  paragraphs: ['Da biste koristili određene funkcije naše platforme, potrebno je da kreirate korisnički račun. Prihvatate da:'],
                  bullets: [
                    'Pružite tačne, potpune i ažurirane informacije tokom registracije',
                    'Održavate sigurnost vašeg naloga i lozinke',
                    'Odmah nas obavijestite o bilo kakvoj neovlaštenoj upotrebi vašeg naloga',
                    'Ste odgovorni za sve aktivnosti koje se dešavaju pod vašim nalogom',
                  ],
                ),
                const LegalSection(
                  title: '4. Kupovina ulaznica',
                  paragraphs: ['Kada kupujete ulaznice preko naše platforme:'],
                  bullets: [
                    'Sve prodaje su konačne osim ako organizator ne navede drugačije',
                    'Cijene ulaznica određuje organizator događaja',
                    'Dodatne naknade za obradu mogu biti primijenjene',
                    'Ulaznice su važeće samo za navedeni događaj, datum i vrijeme',
                    'Falsifikovanje, dupliciranje ili preprodaja ulaznica je strogo zabranjeno',
                  ],
                ),
                const LegalSection(
                  title: '5. Povrati i otkazivanja',
                  paragraphs: ['Politika povrata varira u zavisnosti od organizatora događaja. Molimo vas da pažljivo pročitate politiku povrata prije kupovine. Općenito:'],
                  bullets: [
                    'Povrati se obično ne odobravaju osim u slučaju otkazivanja događaja',
                    'Ako je događaj otkazan, bićete obaviješteni i dobiti pun povrat novca',
                    'Ako je događaj odgođen, ulaznice ostaju važeće za novi datum',
                    'Procesiranje povrata može trajati 7-14 radnih dana',
                  ],
                ),
                const LegalSection(
                  title: '6. Zabranjeno ponašanje',
                  paragraphs: ['Kada koristite našu platformu, saglasni ste da nećete:'],
                  bullets: [
                    'Kršiti bilo koje lokalne, državne ili međunarodne zakone',
                    'Koristiti automatizovane sisteme za kupovinu ulaznica (botove)',
                    'Kupovati ulaznice u cilju preprodaje po višim cijenama',
                    'Ometati ili narušavati sigurnost platforme',
                    'Ometati druge korisnike u korištenju platforme',
                    'Pokušavati dobiti neovlašten pristup sistemu',
                  ],
                ),
                const LegalSection(
                  title: '7. Intelektualna svojina',
                  paragraphs: [
                    'Sav sadržaj na platformi, uključujući tekst, grafiku, logotipe, slike i softver, je vlasništvo eKarta ili naših partnera i zaštićen je autorskim pravima i drugim zakonima o intelektualnoj svojini.',
                  ],
                ),
                const LegalSection(
                  title: '8. Ograničenje odgovornosti',
                  paragraphs: ['eKarta platforma služi kao posrednik između korisnika i organizatora događaja. Ne preuzimamo odgovornost za:'],
                  bullets: [
                    'Kvalitet ili prezentaciju događaja',
                    'Tačnost informacija koje pružaju organizatori',
                    'Otkazivanje ili promjene događaja',
                    'Ponašanje drugih korisnika ili organizatora',
                    'Gubitak ili štetu nastalu korištenjem naših usluga',
                  ],
                ),
                const LegalSection(
                  title: '9. Izmjene uslova',
                  paragraphs: [
                    'Zadržavamo pravo da u bilo kom trenutku izmijenimo ove Uslove korištenja. Sve izmjene će biti objavljene unutar aplikacije sa datumom ažuriranja. Nastavak korištenja platforme nakon izmjena znači da prihvatate nove uslove.',
                  ],
                ),
                const LegalSection(
                  title: '10. Kontakt',
                  paragraphs: [
                    'Ako imate bilo kakvih pitanja o ovim Uslovima korištenja, možete nas kontaktirati na:',
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
