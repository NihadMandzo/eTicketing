# Arhitektura: eTicketing backend — potpuni rewrite (Microservices + EDA)

> Status: finalna arhitektonska odluka — **diplomski rad**
> Datum: 2026-08-04 (druga revizija — potpuni rewrite, ne migracija)
>
> **Promjena u odnosu na prvu verziju ovog dokumenta:** prvobitni plan je predviđao zadržavanje postojećeg monolita (`eTicketing.Api`/`eTicketing.Services`) kao "Identity & Catalog" servisa i dograđivanje novih domena oko njega. Nakon detaljnog definisanja punih zahtjeva (SuperAdmin/Admin modul, preview prije objavljivanja, potpuni nadzor nad eventima/ulaznicama), odlučeno je da se **cijeli backend piše ispočetka**, svi servisi po istom standardu (Minimal API + Repository + Result + PagedResult — vidi [backend-projekt-template.md](backend-projekt-template.md)). Postojeći monolit ostaje u repozitoriju samo kao referenca za kopiranje validacionih pravila/DTO oblika dok traje razvoj, i briše se na kraju (Sprint 5).

---

## 1. Puni funkcionalni zahtjevi (izvor istine)

### SuperAdmin
- Login/registracija (isti auth mehanizam kao i svi drugi, samo je nalog unaprijed seed-ovan).
- Kreira organizaciju **i** njen prvi pristupni nalog (`OrganizationSuperAdmin` ili `OrganizationAdmin`) u jednom koraku.
- Kreira dodatne pristupne naloge za postojeću organizaciju, dodjeljuje rolu (`OrganizationSuperAdmin`/`OrganizationAdmin`).
- Kreira `Admin` naloge (platform staff, manje ovlaštenje od SuperAdmin — ne može kreirati organizacije ni druge Admin naloge).
- Uređuje i briše organizacije.
- Ima **uvid u sve evente i sve ulaznice na platformi** (svih organizacija), i može ih **uređivati i brisati** (administrativni override).

### Admin (platform staff, manje ovlaštenje od SuperAdmin)
- Isti uvid/edit/delete nad svim eventima i ulaznicama kao SuperAdmin.
- **Ne može** kreirati organizacije niti druge Admin naloge (to je isključivo SuperAdmin ovlaštenje) — eksplicitna odluka iz zahtjeva.

### OrganizationSuperAdmin / OrganizationAdmin ("organizator")
- Kreira evente **svoje** organizacije.
- Kreira sektore ulaznica za svoj event (naziv, broj dostupnih ulaznica/kapacitet, cijena).
- Uređuje i briše svoje evente i sektore (uključujući cijenu).
- **Prije objavljivanja i prije snimanja izmjena, obavezan je "preview" korak** (vidi sekciju 4 — arhitektonski obrazac).
- U ovoj iteraciji `OrganizationSuperAdmin` i `OrganizationAdmin` imaju **identična ovlaštenja** nad eventima/sektorima svoje organizacije — razlika između ove dvije role postoji u modelu (za buduću finiju granulaciju), ali se trenutno ne provodi u autorizaciji.
- Organizator **ne** dodaje druge korisnike u svoju organizaciju — to ostaje isključivo SuperAdmin ovlaštenje (vidi gore).

### User (kupac)
- Registracija/login.
- Pregled eventa (samo objavljenih).
- Odabir sektora, količine, kupovina.
- Dobija PDF ulaznicu na email nakon kupovine.
- Na profilu vidi svoje kupljene ulaznice (status, download PDF-a).

---

## 2. Servisna dekompozicija

| Servis | Vlasništvo nad podacima | Ključna odgovornost |
|---|---|---|
| `eTicketing.Gateway` | — (bez baze) | Jedina javna ulazna tačka, JWT validacija, routing |
| `eTicketing.Identity` | `User`, `Role`, `Organization` | Auth (register/login/JWT), upravljanje organizacijama i nalozima (SuperAdmin/Admin/organizator) |
| `eTicketing.Catalog` | `Category`, `Event` | Katalog eventa — kreiranje/uređivanje/preview/publish, javni pregled |
| `eTicketing.Ticketing` | `EventSector`, `Ticket` | Sektori i kapacitet (Redis lock), izdavanje ulaznica, SuperAdmin override nad ulaznicama |
| `eTicketing.Payment` | `Payment`/`Transaction` | Procesiranje plaćanja, **interni servis, bez javne rute** |
| `eTicketing.Notifications` | — (stateless) | RabbitMQ consumer — email verifikacija, potvrda kupovine |
| `eTicketing.PdfGeneration` | — (stateless) | RabbitMQ consumer — generisanje PDF ulaznice, upload na Blob Storage |
| `eTicketing.Analytics` (**opciono, prvo se žrtvuje**) | `AnalyticsDb` | RabbitMQ consumer — agregacija prodaje |

**Zašto Identity i Catalog nisu isti servis (za razliku od prve verzije plana):** sada gradimo od nule, pa nema razloga da ih vještački spajamo — `Identity` (ko smije šta) i `Catalog` (šta postoji) su prirodno različiti bounded konteksti. Cijena razdvajanja je mala: `Catalog` čita `OrganizationId` direktno iz JWT claim-a (nema mrežnog poziva ka `Identity` na uobičajenom putu).

**Zašto `Ticketing` nije spojen sa `Catalog`:** sektori zahtijevaju Redis distribuirano zaključavanje i sinhronu vezu sa `Payment` servisom (circuit breaker) — drugačiji profil opterećenja i drugačiji zahtjevi za otpornost od CRUD-a nad eventima, klasičan razlog za odvojen mikroservis.

**Cross-service poziv koji nije izbjegnut:** kad organizator kreira sektor, `Ticketing` mora znati da li `EventId` stvarno pripada organizaciji pozivaoca. Pošto `Ticketing` ne posjeduje `Event` podatke, radi **sinhroni interni poziv** `Ticketing → Catalog` (`GET /internal/events/{id}`, nije izložen kroz gateway) da dohvati `OrganizationId` eventa i uporedi sa JWT claim-om pozivaoca. Ovaj poziv je zaštićen istom Polly politikom (retry/timeout) kao i `Ticketing → Payment`, iako nije na kritičnom putu kupovine (samo na putu kreiranja sektora od strane organizatora) — vidi [gateway-tok.md](gateway-tok.md).

---

## 3. Uloge i JWT claim-ovi

Isti model rola kao i prije (`RoleType`): `SuperAdmin(1)`, `Admin(2)`, `OrganizationSuperAdmin(3)`, `OrganizationAdmin(4)`, `User(5)`.

JWT (izdat od `eTicketing.Identity`) nosi:
- `sub` — UserId
- `role` — naziv role
- `organizationId` — **prisutan samo** za `OrganizationSuperAdmin`/`OrganizationAdmin` (null za ostale)

Svaki servis koji treba organizaciono-skopiranu autorizaciju (`Catalog`, `Ticketing`) čita `organizationId` direktno iz tokena — nema potrebe za dodatnim pozivom ka `Identity` da se to sazna.

**Autorizacione politike (koriste se dosljedno u svim servisima):**
- `PlatformStaff` — `SuperAdmin` ili `Admin` (pun uvid/edit/delete nad svim eventima/ulaznicama; samo `SuperAdmin` dodatno smije kreirati organizacije/Admin naloge).
- `Organizer` — `OrganizationSuperAdmin` ili `OrganizationAdmin`, **uz provjeru vlasništva** (resurs mora pripadati `organizationId` iz tokena) — izuzev kad je pozivalac `PlatformStaff`, koji uvijek zaobilazi ownership provjeru.
- `Customer` — `User` (kupovina, pregled svojih ulaznica).

---

## 4. Arhitektonski obrazac: Preview prije objavljivanja/izmjene

Zahtjev: "prije objavljivanja i editovanja, treba se uraditi preview" — primjenjuje se i na `Event` i na `EventSector`.

**Dizajn (isti obrazac za oba resursa):**

1. `Event`/`EventSector` imaju `Status` (`Draft`, `Published`).
2. `POST /events` (ili `/sectors`) uvijek kreira kao **`Draft`** — nikad odmah javno vidljivo.
3. `POST /events/preview` (bez `id`, isti oblik zahtjeva kao create/update) — **stateless, ne upisuje ništa u bazu**, samo pokreće istu validaciju koju bi create/update pokrenuli i vraća `Result<PreviewResponse>` (uspjeh + izračunate/prikazne vrijednosti, ili neuspjeh sa jasnim validacionim greškama). Koristi se i za novi event (prije prvog snimanja) i za izmjene postojećeg (organizator pošalje kompletno izmijenjeno stanje, dobije preview prije nego što potvrdi `PUT`).
4. `POST /events/{id}/publish` — eksplicitan prelaz `Draft → Published`, samo za već kreiran (draft) event.
5. `PUT /events/{id}` — snima izmjene (radi bez obzira na status; objavljen event ostaje objavljen nakon izmjene, ne vraća se u draft).
6. `DELETE /events/{id}`.

Ovaj obrazac lijepo demonstrira **Result Pattern** iz [backend-projekt-template.md](backend-projekt-template.md) — `preview` endpoint je čist primjer "validacija bez upisa", gdje `Result<T>` nosi ili validne izračunate podatke ili listu grešaka, bez ijednog exception-a i bez ijednog DB zapisa.

**Javna vidljivost:** `GET /events` (public, kroz gateway bez tokena) vraća **samo** `Status = Published`. `GET /events/mine` (organizator) i `GET /events/all` (`PlatformStaff`) vraćaju sve statuse.

Isti obrazac (`preview` → `publish`) se ponavlja identično za `EventSector` u `eTicketing.Ticketing`.

---

## 5. Sinhroni kritični put kupovine (nepromijenjeno u odnosu na prvu verziju)

1. Kupac bira sektor (`GET /sectors?eventId=`, samo `Published`).
2. `POST /sectors/{id}/hold` — Redis atomarni decrement/lock, TTL 5 min.
3. `POST /purchases` — `Ticketing` sinhrono zove `Payment` (Polly circuit breaker + retry + timeout).
4. Uspjeh → `Ticket` zapis (`Confirmed`), objavljen `TicketPurchased` event na RabbitMQ.
5. Neuspjeh / circuit otvoren → hold se oslobađa, `503` sa jasnom porukom.

## 6. Asinkroni EDA tok (nepromijenjeno)

`TicketPurchased` → `Notifications` (email potvrda) i `PdfGeneration` (PDF + Blob Storage), nezavisno, van kritičnog puta. `Identity` objavljuje `VerificationEmailRequested` pri registraciji, konzumira `Notifications`.

---

## 7. Infrastruktura (nepromijenjeno iz prethodnih odluka)

| Komponenta | Tehnologija | Napomena |
|---|---|---|
| Message broker | RabbitMQ | self-hosted, docker-compose |
| Distribuirano zaključavanje | Redis | samo za hold kapaciteta sektora, ne cache |
| Resiliency | Polly | `Ticketing → Payment` (glavni primjer) i `Ticketing → Catalog` (sekundarni primjer) |
| API Gateway | YARP | jedini javno izložen servis |
| Kontejnerizacija | Docker + docker-compose | dovoljno za obim diplomskog rada, bez Kubernetesa |
| Arhitektura svakog servisa | Minimal API, slojevita struktura u jednom projektu | vidi [backend-projekt-template.md](backend-projekt-template.md) |

---

## 8. Frontend (nepromijenjeno — frontend se NE piše ispočetka)

- `frontend/web` (Angular) — customer storefront, **prilagođava se** novim endpointima (repoint, ne rewrite).
- `frontend/desktop` (Flutter) — Admin/Organizator back-office, već djelomično razvijen (organizacije, kategorije, korisnici) — **prilagođava se** novim endpointima i **dograđuje** novim ekranima (eventi, sektori, preview, SuperAdmin override nad eventima/ulaznicama).
- `frontend/mobile` — van obima.

---

## 9. Puni fazni plan

Detaljan raspis (user stories + taskovi) po sprintu: [SPRINTS/](../SPRINTS/) folder. Kalendarski pregled: [plan-rada-31-dana.md](plan-rada-31-dana.md).

| Sprint | Fokus |
|---|---|
| 1 | Infrastruktura + `eTicketing.Identity` (auth, organizacije, admin nalozi) |
| 2 | `eTicketing.Catalog` (kategorije, eventi, preview/publish) + `eTicketing.Ticketing` (sektori, Redis lock) |
| 3 | Gateway + `eTicketing.Payment` + circuit breaker + RabbitMQ + SuperAdmin override nad ulaznicama + Angular repoint (javne stranice) |
| 4 | `eTicketing.Notifications` + `eTicketing.PdfGeneration` + Angular (kupovina, profil) + Desktop repoint (postojeći ekrani) |
| 5 | Desktop (novi ekrani: eventi, sektori, SuperAdmin override) + hardening + demo priprema |

**Prioritet ako vremena ponestane:** 1) Analytics servis (opcion, prvi se žrtvuje), 2) Stripe integracija → pasti nazad na interni mock plaćanja, 3) Desktop UI polish (funkcionalnost prije estetike). **Ne žrtvuje se:** preview obrazac (ključni funkcionalni zahtjev), Redis lock, circuit breaker, RabbitMQ tok, SuperAdmin override.
