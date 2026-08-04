# SPRINT 2 (Dan 8–12) — eTicketing.Catalog + eTicketing.Ticketing (sektori)

**Cilj sprinta:** isporučiti katalog eventa (sa preview/publish obrascem) i servis za sektore ulaznica (sa Redis zaštitom od overselling-a). Oba servisa oslanjaju se na JWT claim-ove iz `eTicketing.Identity` (Sprint 1) — nema direktne zavisnosti u kodu, samo isti signing key.

---

## US-2.1 — Kategorije eventa

**Kao** platform administrator, **želim** upravljati kategorijama eventa, **kako bi** organizatori mogli klasifikovati svoje evente.

**Acceptance criteria:**
- `GET /categories` javno.
- `POST/PUT/DELETE /categories` — `PlatformStaff` (`SuperAdmin`, `Admin`) samo.

**Taskovi:**
- [ ] T-2.1.1 — Scaffold `eTicketing.Catalog` kao tri projekta (`.Api`/`.Business`/`.Data`) po [backend-setup-guide.md](../docs/backend-setup-guide.md), `CatalogDbContext`, baza `CatalogDb`.
- [ ] T-2.1.2 — `Domain/Entities/Category.cs`, CRUD servis, `PagedResult<CategoryResponse>` za `GET`.
- [ ] T-2.1.3 — `Endpoints/CategoryEndpoints.cs`, autorizacija po AC.

**Eksterni servisi:** **NE.**

---

## US-2.2 — Organizator kreira event uz preview

**Kao** organizator (`OrganizationSuperAdmin`/`OrganizationAdmin`), **želim** kreirati event svoje organizacije i vidjeti preview prije nego što ga sačuvam, **kako bih** bio siguran da su podaci ispravni prije objavljivanja.

**Acceptance criteria:**
- `POST /events/preview` — validira zahtjev (naziv, opis, datum, kategorija) **bez upisa u bazu**, vraća `Result<EventPreviewResponse>`.
- `POST /events` — kreira event kao `Status = Draft`, `OrganizationId` uzet iz JWT claim-a (ne iz body-ja — korisnik ne smije birati tuđu organizaciju).
- Nevažeći podaci (prazan naziv, datum u prošlosti, nepostojeća kategorija) vraćaju istu validacionu poruku i na `preview` i na `create`.

**Taskovi:**
- [ ] T-2.2.1 — `Domain/Entities/Event.cs` (Name, Description, Date, CategoryId, OrganizationId, Status, ImageUrl). EF Core migracija.
- [ ] T-2.2.2 — `UpsertEventRequest`, `EventPreviewResponse`, `EventResponse` DTO-i.
- [ ] T-2.2.3 — `EventService` sa zajedničkom `Validate()` metodom koju koriste `PreviewAsync` i `CreateAsync` (obrazac iz [backend-projekt-template.md](../docs/backend-projekt-template.md), sekcija 8).
- [ ] T-2.2.4 — `Endpoints/EventEndpoints.cs`: `POST /events/preview`, `POST /events` — oboje `RequireAuthorization("Organizer")`.
- [ ] T-2.2.5 — Upload slike eventa — ponovo iskoristiti `BlobStorageService` obrazac iz starog monolita (Azurite emulator).

**Eksterni servisi:** **NE** (Blob Storage je Azurite emulator, self-hosted).

---

## US-2.3 — Objavljivanje, izmjena, brisanje eventa + SuperAdmin uvid nad svim eventima

**Kao** organizator, **želim** objaviti, urediti i obrisati svoj event; **kao** SuperAdmin/Admin, **želim** uvid u sve evente svih organizacija sa mogućnošću izmjene/brisanja.

**Acceptance criteria:**
- `POST /events/{id}/publish` — `Draft → Published`, samo ako event pripada organizaciji pozivaoca (ili pozivalac je `PlatformStaff`).
- `PUT /events/{id}`, `DELETE /events/{id}` — organizator (svoje) ili `PlatformStaff` (bilo koje).
- `GET /events` — javno, **samo** `Published`.
- `GET /events/mine` — organizator, sve svoje (uklj. `Draft`).
- `GET /events/all` — `PlatformStaff`, svi eventi svih organizacija, svi statusi.
- Interni endpoint `GET /internal/events/{id}` — vraća minimalan DTO (`Id`, `OrganizationId`, `Status`), koristi ga `eTicketing.Ticketing` (nije izložen kroz gateway).

**Taskovi:**
- [ ] T-2.3.1 — `PublishAsync`, `UpdateAsync`, `DeleteAsync` u `EventService` — ownership provjera (`entity.OrganizationId == user.GetOrganizationId()`, zaobiđe se za `PlatformStaff`).
- [ ] T-2.3.2 — `GetPublishedAsync` (javno), `GetMineAsync` (organizator), `GetAllAsync` (`PlatformStaff`) — sve vraćaju `PagedResult<EventResponse>`.
- [ ] T-2.3.3 — `GET /internal/events/{id}` — bez autorizacije na nivou gateway-a (gateway ga uopšte ne rutira), ali servis ipak provjerava da poziv dolazi iz internog network-a (ili minimalno — nije osjetljiv podatak, samo `OrganizationId`+`Status`).
- [ ] T-2.3.4 — Autorizacione politike: `Organizer` (uz ownership provjeru u servisu) i `PlatformStaff` (bez provjere).

**Eksterni servisi:** **NE.**

---

## US-2.4 — Organizator kreira sektore uz preview (Ticketing servis)

**Kao** organizator, **želim** kreirati sektore ulaznica (naziv, kapacitet, cijena) za svoj event, uz preview prije snimanja, **kako bih** definisao ponudu ulaznica.

**Acceptance criteria:**
- Isti preview/publish obrazac kao za Event (`POST /sectors/preview`, `POST /sectors`, `POST /sectors/{id}/publish`, `PUT`, `DELETE`).
- Prilikom kreiranja/izmjene, `Ticketing` servis provjerava da `EventId` pripada organizaciji pozivaoca — poziva `GET /internal/events/{id}` na `Catalog` servisu.
- Ako `Catalog` servis ne odgovori (nedostupan), poziv ne uspijeva sa jasnom greškom (ne visi) — zaštićeno Polly retry/timeout politikom.

**Taskovi:**
- [ ] T-2.4.1 — Scaffold `eTicketing.Ticketing` kao tri projekta (`.Api`/`.Business`/`.Data`) po [backend-setup-guide.md](../docs/backend-setup-guide.md), `TicketingDbContext`, baza `TicketingDb`.
- [ ] T-2.4.2 — `Domain/Entities/EventSector.cs` (EventId, Name, Capacity, AvailableCount, Price, Status). `Domain/Entities/Ticket.cs` (skeleton, puni se u Sprint 3).
- [ ] T-2.4.3 — `HttpClientFactory` klijent ka `Catalog` (`CatalogInternalClient`), Polly retry (2-3x) + timeout policy (bez circuit breakera ovdje — to je rezervisano za `Ticketing → Payment` kao glavni primjer u Sprintu 3, ali retry/timeout ima smisla i ovdje).
- [ ] T-2.4.4 — `SectorService` — `Validate()` uključuje poziv `CatalogInternalClient.GetEventAsync(eventId)`, provjera `OrganizationId`.
- [ ] T-2.4.5 — `Endpoints/SectorEndpoints.cs`: `preview`, `create`, `publish`, `update`, `delete`, `GET /sectors?eventId=` (javno, samo sektori `Published` eventa).

**Eksterni servisi:** **NE.**

---

## US-2.5 — Redis lock za kapacitet sektora (sprječavanje overselling-a)

**Kao** kupac, **želim** da sistem spriječi kupovinu više ulaznica nego što ih ima na raspolaganju, čak i kad više ljudi kupuje istovremeno.

**Acceptance criteria:**
- `POST /sectors/{id}/hold` (količina) — atomarno umanjuje `AvailableCount` uz TTL 5 min, vraća `holdId`.
- N paralelnih zahtjeva za isti sektor sa graničnim preostalim kapacitetom nikad ukupno ne prekorače `Capacity`.
- Istek TTL-a vraća kapacitet.

**Taskovi:**
- [ ] T-2.5.1 — `Infrastructure/Redis/RedisSectorCapacityLock.cs` (`ISectorCapacityLock`) — Lua skripta za atomarni decrement sa TTL-om.
- [ ] T-2.5.2 — `POST /sectors/{id}/hold` endpoint, `RequireAuthorization()` (bilo koji prijavljeni `User`).
- [ ] T-2.5.3 — Integracioni test: N paralelnih zahtjeva (npr. `Task.WhenAll`) protiv sektora sa malim preostalim kapacitetom, provjera da zbir uspješnih holdova ne prelazi `Capacity`.
- [ ] T-2.5.4 — Sprint review — demo: kreiranje eventa → objavljivanje → kreiranje sektora → paralelna kupovina ne overselluje.

**Eksterni servisi:** **NE** (Redis self-hosted iz Sprint 1 infrastrukture).

---

### Definition of done za Sprint 2

`eTicketing.Catalog` i `eTicketing.Ticketing` rade samostalno. Organizator kroz API kreira/pregleda/objavljuje/uređuje/briše evente i sektore uz preview korak. SuperAdmin/Admin imaju pun uvid nad svim eventima. Kapacitet sektora je zaštićen Redis lockom, dokazano testom konkurencije.
