# Plan rada — 31 dan (diplomski rad, potpuni backend rewrite)

> Prati [arhitektura-migracija-mikroservisi-eda.md](arhitektura-migracija-mikroservisi-eda.md) (druga revizija — cijeli backend se piše ispočetka).
> Struktura: **sprint = 5 radnih dana + 2 dana odmora**. 31 dan = 4 puna sprinta (28 dana) + skraćeni finalni sprint (3 dana, bez odmora).
> Backend: **Minimal API + Repository + Result Pattern + PagedResult** u svakom servisu — vidi [backend-projekt-template.md](backend-projekt-template.md).
> Frontend: **NE piše se ispočetka** — Angular (`frontend/web`) i Flutter Desktop (`frontend/desktop`) se prilagođavaju novim endpointima i dograđuju novim ekranima.
> Detaljan raspis user story-ja i taskova po sprintu: [SPRINTS/](../SPRINTS/) folder.

## Pregled

| Sprint | Dani | Fokus | Ishod na kraju sprinta |
|---|---|---|---|
| Sprint 1 | 1–5 (odmor 6–7) | Infrastruktura + `eTicketing.Identity` (auth, organizacije, admin nalozi) | Registracija/login rade, SuperAdmin kreira organizacije/organizatore/Admin naloge |
| Sprint 2 | 8–12 (odmor 13–14) | `eTicketing.Catalog` (kategorije, eventi, preview/publish) + `eTicketing.Ticketing` (sektori, Redis lock) | Organizator kreira event i sektore uz preview, kapacitet je zaštićen od overselling-a |
| Sprint 3 | 15–19 (odmor 20–21) | Gateway + `eTicketing.Payment` + circuit breaker + RabbitMQ + SuperAdmin override ulaznica + Angular repoint (javne stranice) | Kompletan sinhroni tok kupovine radi end-to-end (bez UI-a za checkout još) |
| Sprint 4 | 22–26 (odmor 27–28) | `eTicketing.Notifications` + `eTicketing.PdfGeneration` + Angular (kupovina, profil) + Desktop repoint | Kupac kroz Angular kupuje ulaznicu i dobija PDF/email; Desktop app radi sa novim backendom |
| Sprint 5 | 29–31 (bez odmora) | Desktop: eventi/sektori/SuperAdmin override ekrani + hardening + demo priprema | Sistem pokriva sve uloge kroz UI, `docker-compose up` radi od nule |

**Prioritet ako vremena ponestane:** 1) `eTicketing.Analytics` (opcion, prvi se žrtvuje), 2) Stripe integracija → pasti na interni mock plaćanja, 3) Desktop UI estetika. **Ne žrtvuje se:** preview obrazac, Redis lock, circuit breaker, RabbitMQ tok, SuperAdmin override nad eventima/ulaznicama — ovo su srž teze i eksplicitni funkcionalni zahtjevi.

---

## Sprint 1 (Dan 1–5) — Infrastruktura + eTicketing.Identity

| Dan | Zadaci |
|---|---|
| 1 | `docker-compose.yml` (SQL Server, RabbitMQ, Redis). `eTicketing.Contracts` (Result, PagedResult, Events skeleton — vidi [backend-projekt-template.md](backend-projekt-template.md)). Odluka: stari monolit ostaje privremeno u repou kao referenca, briše se u Sprint 5. Scaffold `eTicketing.Identity` po template strukturi. |
| 2 | `User`, `Role` entiteti, EF Core migracija. `POST /auth/register` (rola `User`), `POST /auth/login` (JWT sa `sub`/`role`/`organizationId` claim-ovima). Role seed (`SuperAdmin`, `Admin`, `OrganizationSuperAdmin`, `OrganizationAdmin`, `User`). |
| 3 | `Organization` entitet. `POST /organizations` — transakciono kreira organizaciju **i** prvog organizatora u jednom pozivu (`SuperAdmin` samo). Autorizacione politike (`PlatformStaff`, `Organizer`, `Customer`). |
| 4 | `POST /admins` (SuperAdmin kreira Admin nalog). `POST /organizations/{id}/users` (SuperAdmin dodaje dodatnog organizatora postojećoj organizaciji). `PUT`/`DELETE /organizations/{id}`. |
| 5 | Global exception handler + Result pattern konzistentno kroz sve endpointe. Testiranje: register→login, SuperAdmin kreira org+admin nalog, provjera JWT claim-ova. Sprint review. |

**Definition of done:** `eTicketing.Identity` radi samostalno; SuperAdmin može kreirati organizacije (sa organizatorom), dodatne organizatore i Admin naloge; svi korisnici se mogu prijaviti i dobiti ispravan JWT. Puni raspis: [SPRINT_1.md](../SPRINTS/SPRINT_1.md).

*Odmor: Dan 6–7.*

---

## Sprint 2 (Dan 8–12) — eTicketing.Catalog + eTicketing.Ticketing (sektori)

| Dan | Zadaci |
|---|---|
| 8 | Scaffold `eTicketing.Catalog`. `Category` entitet + CRUD (`PlatformStaff` piše, javno čitanje). |
| 9 | `Event` entitet (`OrganizationId`, `CategoryId`, `Status`). `POST /events` (Draft), `POST /events/preview` (stateless validacija), `PUT /events/{id}`, `DELETE /events/{id}` — sve org-skopirano. |
| 10 | `POST /events/{id}/publish`. `GET /events/mine` (organizator), `GET /events/all` (`PlatformStaff`, sve organizacije, svi statusi), `GET /events` (javno, samo `Published`). Interni endpoint `GET /internal/events/{id}` za Ticketing. |
| 11 | Scaffold `eTicketing.Ticketing`. `EventSector` entitet (`EventId`, `Name`, `Capacity`, `AvailableCount`, `Price`, `Status`). CRUD + preview + publish, isti obrazac kao Event. Sinhroni poziv `Ticketing → Catalog` (`GET /internal/events/{id}`) za provjeru vlasništva, sa Polly retry/timeout. |
| 12 | Redis integracija — atomarni hold kapaciteta (`POST /sectors/{id}/hold`, TTL 5 min). Test konkurencije (N paralelnih zahtjeva, provjera da se `Capacity` ne prekorači). |

**Definition of done:** organizator kroz API kreira event (uz preview), objavljuje ga, dodaje sektore (uz preview), kapacitet sektora se ne može prekoračiti pod paralelnim opterećenjem. Puni raspis: [SPRINT_2.md](../SPRINTS/SPRINT_2.md).

*Odmor: Dan 13–14.*

---

## Sprint 3 (Dan 15–19) — Gateway + Payment + circuit breaker + RabbitMQ

| Dan | Zadaci |
|---|---|
| 15 | `eTicketing.Gateway` (YARP) — rute ka `Identity`, `Catalog`, `Ticketing` (`Payment` bez javne rute). JWT validacija, CORS. Dodati u `docker-compose`. |
| 16 | `eTicketing.Payment` — `Payment`/`Transaction` entitet, `POST /payments` (interni mock ili Stripe test mode — odluka na implementaciji). Polly circuit breaker + retry + timeout na `Ticketing → Payment` pozivu. Ručni test: ugasiti Payment kontejner, provjeriti fail-fast ponašanje. |
| 17 | RabbitMQ setup u `Contracts`. `POST /purchases` u Ticketing: hold → poziv Payment → `Ticket` zapis → publish `TicketPurchased`. End-to-end test bez UI-a (Postman). |
| 18 | `GET /tickets/all`, `PUT /tickets/{id}`, `DELETE /tickets/{id}` u Ticketing — SuperAdmin/Admin override nad svim ulaznicama. |
| 19 | Angular repoint: javne stranice (`home`, `events`, `search`) i auth (`login`/`register`) prebačene na novi Gateway. Sprint review — demo kompletnog sinhronog toka. |

**Definition of done:** kompletan sinhroni tok kupovine radi end-to-end (bez checkout UI-a), circuit breaker demonstrira graceful degradaciju, SuperAdmin ima override nad ulaznicama, Angular javne stranice rade protiv novog backenda. Puni raspis: [SPRINT_3.md](../SPRINTS/SPRINT_3.md).

*Odmor: Dan 20–21.*

---

## Sprint 4 (Dan 22–26) — Notifications + PDF + Angular kupovina/profil + Desktop repoint

| Dan | Zadaci |
|---|---|
| 22 | `eTicketing.Notifications` — RabbitMQ consumer, email verifikacija (registracija) + potvrda kupovine. |
| 23 | `eTicketing.PdfGeneration` — RabbitMQ consumer, QuestPDF, upload na Blob Storage, ažuriranje statusa ulaznice. |
| 24 | Angular — pregled sektora na eventu, `cart` (odabir + hold + odbrojavanje), poziv kroz Gateway. |
| 25 | Angular — stranica plaćanja (mock/Stripe forma), rukovanje `503` od circuit breakera. |
| 26 | Angular — profil "moje ulaznice" (lista, status, polling, PDF download). Desktop — repoint postojećih ekrana (`organizations_screen`, `categories_screen`, `users_screen`, `login_screen`) na novi Gateway/Identity/Catalog. |

**Definition of done:** kupac kroz Angular kompletno kupuje ulaznicu i dobija email+PDF; postojeći Desktop ekrani rade protiv novog backenda. Puni raspis: [SPRINT_4.md](../SPRINTS/SPRINT_4.md).

*Odmor: Dan 27–28.*

---

## Sprint 5 (Dan 29–31) — Desktop dovršetak + hardening + demo (skraćeni sprint)

| Dan | Zadaci |
|---|---|
| 29 | Desktop — `events_screen.dart` (organizator: kreiranje/preview/publish/edit/delete eventa) + SuperAdmin "svi eventi" pregled sa edit/delete. |
| 30 | Desktop — ekran sektora (kreiranje/preview/publish/edit/delete, cijena) + SuperAdmin "sve ulaznice" pregled sa edit/delete. Čisti `docker-compose up` test od nule. |
| 31 | Hardening (exception handleri, Serilog, `/health` po servisu ako nije već urađeno usput) + e2e test kroz sve tri uloge (kupac/organizator/SuperAdmin) + priprema demonstracije (circuit breaker uživo, overselling test) + buffer. |

**Definition of done:** sve tri uloge (kupac, organizator, SuperAdmin) imaju kompletnu UI podršku, sistem se pouzdano pokreće od nule, demonstracija otpornosti je uvježbana. Puni raspis: [SPRINT_5.md](../SPRINTS/SPRINT_5.md).
