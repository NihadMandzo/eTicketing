import 'package:flutter/material.dart';

import '../widgets/legal_content_widgets.dart';
import '../widgets/responsive_page.dart';

/// "Pomoć" — FAQ content ported verbatim from `frontend/web`'s
/// `pages/help/help.component.html`, for feature parity between the two
/// apps (web had this page, mobile didn't).
class HelpScreen extends StatelessWidget {
  const HelpScreen({super.key});

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      appBar: AppBar(title: const Text('Pomoć')),
      body: SafeArea(
        child: SingleChildScrollView(
          child: ResponsivePage(
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: const [
                FaqGroup(
                  title: 'Kupovina ulaznica',
                  items: [
                    FaqItem(
                      question: 'Kako mogu kupiti ulaznicu?',
                      answer:
                          'Kupovina ulaznice je jednostavna. Pronađite događaj koji vas zanima, kliknite na "Kupi ulaznice", izaberite broj ulaznica, unesite svoje podatke i izvršite plaćanje. Ulaznica će vam biti poslana na email.',
                    ),
                    FaqItem(
                      question: 'Koje načine plaćanja prihvatate?',
                      answer: 'Prihvatamo sve veće kreditne i debitne kartice (Visa, Mastercard, AmEx), kao i online plaćanje putem PayPal-a.',
                    ),
                    FaqItem(
                      question: 'Da li dobijam potvrdu kupovine?',
                      answer: 'Da, nakon uspješne kupovine odmah ćete dobiti potvrdu na email adresu koju ste naveli prilikom kupovine.',
                    ),
                    FaqItem(
                      question: 'Mogu li kupiti ulaznice bez registracije?',
                      answer: 'Možete pregledati događaje bez registracije, ali za kupovinu ulaznica potrebno je da kreirate račun.',
                    ),
                  ],
                ),
                FaqGroup(
                  title: 'Ulaznice i povrati',
                  items: [
                    FaqItem(
                      question: 'Gdje mogu pronaći svoje ulaznice?',
                      answer: 'Sve kupljene ulaznice možete pronaći u vašem profilu, pod sekcijom "Moje ulaznice". Također, svaka ulaznica vam je poslana i na email.',
                    ),
                    FaqItem(
                      question: 'Mogu li vratiti ulaznicu?',
                      answer: 'Politika povrata zavisi od organizatora događaja. Većina organizatora dozvoljava povrat do 7 dana prije događaja. Provjerite uslove prilikom kupovine.',
                    ),
                    FaqItem(
                      question: 'Šta ako sam izgubio ulaznicu?',
                      answer: 'Ne brinite! Sve vaše ulaznice su sačuvane u vašem profilu i možete ih ponovo preuzeti bilo kada. Također možete zatražiti ponovo slanje na email.',
                    ),
                    FaqItem(
                      question: 'Mogu li prenijeti ulaznicu na drugu osobu?',
                      answer: 'Da, možete prenijeti ulaznicu na drugu osobu putem funkcije "Prenesi ulaznicu" u vašem profilu. Primatelj će dobiti email sa uputstvima.',
                    ),
                  ],
                ),
                FaqGroup(
                  title: 'Korisnički račun',
                  items: [
                    FaqItem(
                      question: 'Kako da kreiram račun?',
                      answer: 'Kliknite na dugme "Registracija", unesite potrebne podatke i potvrdite vašu email adresu.',
                    ),
                    FaqItem(
                      question: 'Zaboravio sam lozinku, šta da radim?',
                      answer: 'Na stranici za prijavu, kliknite na "Zaboravili ste lozinku?" i slijedite uputstva za resetovanje lozinke.',
                    ),
                    FaqItem(
                      question: 'Kako mogu promijeniti svoje podatke?',
                      answer: 'Prijavite se na vaš račun i idite na "Lični podaci" u Profilu gdje možete ažurirati svoje podatke.',
                    ),
                    FaqItem(
                      question: 'Da li su moji podaci sigurni?',
                      answer: 'Apsolutno. Koristimo najnovije sigurnosne protokole i sve vaše podatke čuvamo enkriptovane. Više informacija možete pronaći u našoj politici privatnosti.',
                    ),
                  ],
                ),
                FaqGroup(
                  title: 'Tehnička podrška',
                  items: [
                    FaqItem(
                      question: 'Aplikacija ne radi pravilno, šta da radim?',
                      answer: 'Prvo probajte ponovo pokrenuti aplikaciju. Ako problem i dalje postoji, kontaktirajte našu podršku.',
                    ),
                    FaqItem(
                      question: 'Nisam dobio email sa ulaznicom?',
                      answer: 'Provjerite spam/junk folder. Ako email nije tu, prijavite se na vaš račun i preuzmite ulaznice direktno sa platforme.',
                    ),
                    FaqItem(
                      question: 'Kako mogu kontaktirati podršku?',
                      answer: 'Možete nas kontaktirati putem email-a na info@ekarta.ba ili telefona +387 33 123 456 radnim danima od 9:00 do 17:00.',
                    ),
                    FaqItem(
                      question: 'Koliko dugo traje odgovor podrške?',
                      answer: 'Trudimo se da odgovorimo na sve upite u roku od 24 sata radnim danima.',
                    ),
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
