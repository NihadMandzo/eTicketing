# SPRINT 4 (Dan 22–26) — Notifications + PDF + Angular kupovina/profil + Desktop repoint

**Cilj sprinta:** zatvoriti asinkroni EDA tok (email + PDF), dovršiti kupovinu i profil na Angular webu, i prebaciti postojeće Desktop ekrane na novi backend.

---

## US-4.1 — Notifications servis

**Kao** korisnik, **želim** dobiti email potvrdu prilikom registracije i kupovine, **kako bih** bio obaviješten bez provjeravanja aplikacije.

**Acceptance criteria:**
- Konzumira `VerificationEmailRequested` (objavljen u Sprintu 1 od `Identity`) i `TicketPurchased` (objavljen u Sprintu 3 od `Ticketing`).
- Šalje odgovarajući email za svaki.

**Taskovi:**
- [ ] T-4.1.1 — Scaffold `eTicketing.Notifications` (Worker Service, RabbitMQ consumer).
- [ ] T-4.1.2 — `MailKit` integracija, HTML template-i (verifikacija, potvrda kupovine).
- [ ] T-4.1.3 — Consumer za `VerificationEmailRequested` i `TicketPurchased` sa `eticketing.events` exchange-a.
- [ ] T-4.1.4 — Ručni test: registracija → email sa OTP/verifikacionim linkom stvarno stigne (vidi opcije ispod).

**Eksterni servisi:** **DA — izbor SMTP mehanizma:**
1. **MailHog/Papercut** (preporuka za razvoj) — lokalni SMTP catcher, dodatni Docker kontejner, potpuno self-hosted.
2. **Mailtrap.io** — besplatan sandbox, pravi SMTP protokol, email ostaje u Mailtrap inboxu.
3. **Gmail SMTP + App Password** — ako želiš da email stvarno stigne primaocu.

---

## US-4.2 — PdfGeneration servis

**Kao** kupac, **želim** dobiti PDF ulaznicu nakon kupovine, **kako bih** je mogao preuzeti/odštampati.

**Acceptance criteria:**
- Konzumira `TicketPurchased`, generiše PDF (naziv eventa, sektor, kod ulaznice), sprema na Blob Storage.
- Status ulaznice u `Ticketing` se ažurira na "spremna" kad je PDF gotov.

**Taskovi:**
- [ ] T-4.2.1 — Scaffold `eTicketing.PdfGeneration` (Worker Service, RabbitMQ consumer).
- [ ] T-4.2.2 — `QuestPDF` integracija, QR kod opciono (`QRCoder`).
- [ ] T-4.2.3 — Upload na Blob Storage (Azurite), po uzoru na postojeći `BlobStorageService`.
- [ ] T-4.2.4 — Nakon uspješnog generisanja, poziv ka `Ticketing` (`PUT /tickets/{id}/mark-ready` — interni endpoint) ili publish povratnog eventa `TicketPdfReady` koji `Ticketing` konzumira — odabrati i dokumentovati pristup u kodu.

**Eksterni servisi:** **NE** (QuestPDF Community licenca besplatna za obim diplomskog rada; Azurite emulator).

---

## US-4.3 — Angular: pregled sektora i korpa

**Kao** kupac, **želim** vidjeti sektore eventa i rezervisati željenu količinu, **kako bih** imao vremena da završim plaćanje.

**Acceptance criteria:**
- Stranica detalja eventa prikazuje sektore (naziv, cijena, dostupnost) sa `eTicketing.Ticketing` kroz Gateway.
- `cart` stranica (trenutno prazna) omogućava odabir sektora+količine, poziva `hold`, prikazuje odbrojavanje (TTL).

**Taskovi:**
- [ ] T-4.3.1 — Angular `SectorService` (`GET /api/sectors?eventId=`).
- [ ] T-4.3.2 — `sector-list` komponenta unutar detalja eventa.
- [ ] T-4.3.3 — `cart` komponenta — odabir količine, poziv `POST /api/sectors/{id}/hold`.
- [ ] T-4.3.4 — Countdown timer komponenta, rukovanje istekom holda.

**Eksterni servisi:** **NE.**

---

## US-4.4 — Angular: plaćanje

**Kao** kupac, **želim** unijeti podatke za plaćanje i izvršiti kupovinu, **kako bih** dobio ulaznicu.

**Acceptance criteria:**
- Forma za plaćanje (mock ili Stripe, prema odluci iz US-3.2) poziva `POST /api/purchases`.
- Uspjeh vodi na potvrdu; neuspjeh (uklj. `503` od circuit breakera) prikazuje jasnu poruku.

**Taskovi:**
- [ ] T-4.4.1 — `payment` komponenta.
- [ ] T-4.4.2 — Poziv `POST /api/purchases` (holdId + podaci za plaćanje).
- [ ] T-4.4.3 — Rukovanje `503` — "Plaćanje trenutno nije dostupno, pokušajte kasnije" umjesto generičke greške.

**Eksterni servisi:** zavisi od US-3.2 odluke (Stripe.js ako je odabran Stripe test mode; inače **NE**).

---

## US-4.5 — Angular: profil "moje ulaznice"

**Kao** kupac, **želim** vidjeti svoje kupljene ulaznice na profilu, **kako bih** znao kad mogu preuzeti PDF.

**Acceptance criteria:**
- `tickets` stranica (trenutno prazna) prikazuje `GET /api/tickets/mine`, status ("u obradi"/"spremna"), link za preuzimanje PDF-a kad je spremna.

**Taskovi:**
- [ ] T-4.5.1 — `tickets` komponenta (lista + status badge).
- [ ] T-4.5.2 — Jednostavan polling (svakih 5s) dok status nije "spremna".
- [ ] T-4.5.3 — Banner nakon kupovine ("Potvrda je poslana na email, ulaznica se generiše").

**Eksterni servisi:** **NE.**

---

## US-4.6 — Desktop repoint postojećih ekrana

**Kao** SuperAdmin/organizator, **želim** da postojeći Desktop ekrani (organizacije, kategorije, korisnici, login) rade protiv novog backenda, **kako** prelazak na novu arhitekturu ne bi pokvario već razvijenu funkcionalnost.

**Acceptance criteria:**
- `login_screen.dart`, `organizations_screen.dart`, `categories_screen.dart`, `users_screen.dart` rade protiv Gateway-a (`Identity`/`Catalog`).
- Sve postojeće radnje (kreiranje organizacije+organizatora, CRUD kategorija, pregled korisnika po roli) rade identično kao prije.

**Taskovi:**
- [ ] T-4.6.1 — Ažurirati baznu URL konfiguraciju u Desktop app-u da pokazuje na Gateway.
- [ ] T-4.6.2 — Provjeriti/ažurirati `organization_provider.dart`, `user_provider.dart` — novi oblik odgovora (`PagedResult` iz novog backenda mora odgovarati očekivanom `Items/TotalCount/Page/PageSize` obliku — vidi [backend-projekt-template.md](../docs/backend-projekt-template.md) sekcija 0/6).
- [ ] T-4.6.3 — Ažurirati `admin_user_response.dart`, `organization_response.dart` modele ako se oblik JSON-a razlikuje od starog backenda.
- [ ] T-4.6.4 — Regresioni test: login kao SuperAdmin, kreiranje organizacije+organizatora, pregled korisnika, CRUD kategorija — sve kroz Desktop app protiv novog backenda.

**Eksterni servisi:** **NE.**

---

### Definition of done za Sprint 4

Kupac kroz Angular kompletno pregleda event, rezerviše sektor, plaća, dobija email potvrdu i PDF ulaznicu, i vidi je na profilu. Postojeći Desktop ekrani (organizacije/kategorije/korisnici/login) rade protiv novog backenda bez regresije.
