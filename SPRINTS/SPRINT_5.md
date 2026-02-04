# SPRINT 5 (Dan 29–31) — Desktop dovršetak + hardening + demo (skraćeni sprint, bez odmora)

**Cilj sprinta:** dograditi Desktop app novim ekranima (eventi, sektori, SuperAdmin override), potvrditi da se cijeli sistem pouzdano pokreće od nule, i pripremiti demonstraciju za odbranu.

---

## US-5.1 — Desktop: organizator upravlja eventima + SuperAdmin uvid nad svim eventima

**Kao** organizator, **želim** kroz Desktop app kreirati/uređivati/objavljivati evente uz preview; **kao** SuperAdmin/Admin, **želim** uvid u sve evente svih organizacija sa mogućnošću izmjene/brisanja.

**Acceptance criteria:**
- Novi `events_screen.dart` (po uzoru na postojeći `organizations_screen.dart`/`categories_screen.dart`): lista eventa, forma za kreiranje/izmjenu, dugme "Preview" prije snimanja, dugme "Objavi".
- Organizator vidi samo svoje evente (`GET /events/mine`); SuperAdmin/Admin vidi sve (`GET /events/all`), sa mogućnošću izmjene/brisanja bilo kojeg eventa.

**Taskovi:**
- [ ] T-5.1.1 — `frontend/desktop/lib/screens/events_screen.dart` — grid/lista, pretraga, paginacija (isti UX obrazac kao `organizations_screen.dart`).
- [ ] T-5.1.2 — `event_upsert_dialog.dart` — forma sa dugmetom "Preview" (poziva `POST /events/preview`, prikazuje rezultat u dijalogu) prije "Sačuvaj"/"Objavi".
- [ ] T-5.1.3 — `event_provider.dart` (`GET /events/mine`, `GET /events/all` za `PlatformStaff`, `POST /events/preview`, `POST /events`, `POST /events/{id}/publish`, `PUT`, `DELETE`).
- [ ] T-5.1.4 — Uslovno grananje u UI (rola iz JWT-a): organizator vidi "moje evente", SuperAdmin/Admin vidi "svi eventi" sa oznakom organizacije po eventu.
- [ ] T-5.1.5 — Stavka u `app_sidebar.dart` vidljiva odgovarajućim rolama.

**Eksterni servisi:** **NE.**

---

## US-5.2 — Desktop: organizator upravlja sektorima + SuperAdmin uvid nad svim ulaznicama

**Kao** organizator, **želim** kroz Desktop app kreirati/uređivati sektore (naziv, kapacitet, cijena) uz preview; **kao** SuperAdmin/Admin, **želim** uvid u sve ulaznice sa mogućnošću izmjene/brisanja.

**Acceptance criteria:**
- Unutar detalja eventa (iz US-5.1), organizator vidi listu sektora i formu za dodavanje/izmjenu sa "Preview" korakom.
- Novi ekran (SuperAdmin/Admin): lista svih ulaznica platforme (`GET /tickets/all`), sa mogućnošću izmjene statusa/brisanja.

**Taskovi:**
- [ ] T-5.2.1 — `sector_upsert_dialog.dart` sa "Preview" dugmetom (`POST /sectors/preview`).
- [ ] T-5.2.2 — `sector_provider.dart` (`GET/POST/PUT/DELETE /sectors`, `POST /sectors/{id}/publish`).
- [ ] T-5.2.3 — Lista sektora unutar ekrana detalja eventa, prikaz prodatih/preostalih mjesta.
- [ ] T-5.2.4 — `tickets_screen.dart` (SuperAdmin/Admin) — lista svih ulaznica, filter po eventu/korisniku, izmjena statusa, brisanje.
- [ ] T-5.2.5 — Čisti `docker-compose up` test od nule (obrisati volumene) — svi servisi (`Identity`, `Catalog`, `Ticketing`, `Payment`, `Notifications`, `PdfGeneration`, `Gateway`) se podignu bez ručne intervencije. Seed podaci za demo (SuperAdmin nalog, jedna organizacija, jedan event, sektori).

**Eksterni servisi:** **NE.**

---

## US-5.3 — Hardening, end-to-end test i priprema demonstracije

**Kao** autor rada, **želim** ojačan i testiran sistem sa uvježbanom demonstracijom, **kako bih** uspješno odbranio rad.

**Acceptance criteria:**
- Svaki servis ima `GlobalExceptionHandler`, `/health` endpoint, Serilog logovanje.
- Kompletan korisnički put proveden bez grešaka za sve tri uloge (kupac, organizator, SuperAdmin).
- Demonstracija otpornosti (circuit breaker) i konkurencije (Redis lock) je uvježbana.
- Stari monolit (`backend/eTicketing.Api` i dr.) je uklonjen iz repozitorija (ili jasno označen kao arhivski, van solution-a) nakon što je potvrđeno da novi backend ima punu funkcionalnu paritetnost.

**Taskovi:**
- [ ] T-5.3.1 — Proći kroz sve servise, potvrditi `GlobalExceptionHandler`, `/health`, Serilog (konzola+fajl) — dopuniti gdje nedostaje (trebalo je biti urađeno usput u Sprint 1-4, ovo je finalna provjera).
- [ ] T-5.3.2 — E2e checklist: kupac (pregled → hold → plaćanje → email → PDF → profil), organizator (login → kreiranje eventa uz preview → objava → kreiranje sektora uz preview), SuperAdmin (kreiranje organizacije+organizatora, kreiranje Admin naloga, uvid/izmjena/brisanje tuđih eventa i ulaznica).
- [ ] T-5.3.3 — Popraviti pronađene bugove.
- [ ] T-5.3.4 — Ukloniti stari monolit iz repozitorija (`backend/eTicketing.Api`, `eTicketing.Services`, `eTicketing.Model`, `eTicketing.EmailWorker`) nakon potvrde pariteta; ažurirati `eTicketing.sln`.
- [ ] T-5.3.5 — Pripremiti demonstraciju otpornosti: `docker stop` na Payment kontejneru uživo, izvršiti kupovinu, pokazati grešku i oporavak nakon `docker start`.
- [ ] T-5.3.6 — Pripremiti demonstraciju konkurencije: skripta sa paralelnim zahtjevima na sektor sa malo preostalih mjesta, pokazati da broj prodatih ulaznica nikad ne prelazi kapacitet.
- [ ] T-5.3.7 — Finalizovati arhitekturne dijagrame u [arhitektura-migracija-mikroservisi-eda.md](../docs/arhitektura-migracija-mikroservisi-eda.md) i [gateway-tok.md](../docs/gateway-tok.md) da odgovaraju stvarno implementiranom sistemu; proba demonstracije od početka do kraja.

**Eksterni servisi:** **NE.**

---

### Definition of done za Sprint 5

Sve tri uloge (kupac, organizator, SuperAdmin) imaju kompletnu funkcionalnost dostupnu kroz UI (Angular + Desktop). Sistem se pokreće sa `docker-compose up` bez ručne intervencije. Stari monolit je uklonjen. Demonstracija otpornosti i konkurencije je uvježbana i pouzdana.
