# SPRINT 3 (Dan 15–19) — Gateway + Payment + circuit breaker + RabbitMQ + override

**Cilj sprinta:** ujediniti sve servise iza gateway-a, dodati Payment servis sa circuit breakerom, uvesti RabbitMQ i kompletirati sinhroni tok kupovine end-to-end, dodati SuperAdmin override nad ulaznicama, i početi repoint Angular weba.

---

## US-3.1 — API Gateway

**Kao** developer, **želim** jedinstvenu javnu ulaznu tačku za sve servise, **kako bi** klijenti imali jedan URL i konzistentnu JWT validaciju.

**Acceptance criteria:**
- Gateway rutira ka `Identity`, `Catalog`, `Ticketing`. `Payment` **nema** javnu rutu.
- JWT validacija na gateway nivou (isti signing key kao `Identity`).
- Samo `eTicketing.Gateway` ima publikovan port ka hostu u `docker-compose.yml`.

**Taskovi:**
- [ ] T-3.1.1 — Kreirati `eTicketing.Gateway` (YARP — `Yarp.ReverseProxy`).
- [ ] T-3.1.2 — Rute/clusteri u `appsettings.json` po tabeli iz [gateway-tok.md](../docs/gateway-tok.md) sekcija 1.
- [ ] T-3.1.3 — JWT Bearer autentikacija na gateway nivou.
- [ ] T-3.1.4 — CORS konfiguracija na gateway-u (za Angular i Desktop origin-e).
- [ ] T-3.1.5 — Dodati gateway u `docker-compose.yml`, ukloniti publikovane portove svih ostalih servisa ka hostu.

**Eksterni servisi:** **NE.**

---

## US-3.2 — Payment servis

**Kao** kupac, **želim** da moja kupovina bude naplaćena putem izolovanog servisa za plaćanja, **kako bi** logika plaćanja bila odvojena od ostatka sistema.

**Acceptance criteria:**
- `POST /payments` (interno, poziva ga samo `Ticketing`) prima iznos i podatke kartice, vraća uspjeh/neuspjeh.
- `Payment` ima vlastitu bazu (`PaymentDb`).

**Taskovi:**
- [ ] T-3.2.1 — Scaffold `eTicketing.Payment` kao tri projekta (`.Api`/`.Business`/`.Data`) po [backend-setup-guide.md](../docs/backend-setup-guide.md).
- [ ] T-3.2.2 — `Domain/Entities/Payment.cs` (Amount, Status, OrderRef, CreatedAt).
- [ ] T-3.2.3 — `POST /payments` — **odluka o pristupu** (vidi napomenu ispod).
- [ ] T-3.2.4 — U `docker-compose.yml` osigurati da `eTicketing.Payment` nema publikovan port ka hostu.

**Eksterni servisi:** **OPCIONO:**
- **Bez eksternog servisa (preporuka za brzinu):** interni mock — deterministički/nasumično vraća uspjeh/neuspjeh (npr. broj kartice koji završava na "0000" simulira neuspjeh, korisno za demonstraciju circuit breakera).
- **Sa eksternim servisom:** Stripe test mode (besplatno, test kartice koje namjerno fail-uju, `Stripe.net` NuGet paket).

---

## US-3.3 — Circuit breaker na Ticketing → Payment pozivu

**Kao** sistem, **želim** da poziv ka Payment servisu bude zaštićen circuit breaker politikom, **kako** kvar tog servisa ne bi oborio cijeli sistem.

**Acceptance criteria:**
- Kad je `Payment` nedostupan, `Ticketing` nakon nekoliko uzastopnih grešaka odmah vraća grešku (fail-fast), oslobađa hold na kapacitetu.
- Nakon oporavka `Payment`-a, sistem se sam oporavlja bez restarta.

**Taskovi:**
- [ ] T-3.3.1 — `Microsoft.Extensions.Http.Resilience` (Polly) u `eTicketing.Ticketing`, `HttpClientFactory` za `PaymentClient`: circuit breaker (5 grešaka/30s → otvoren 15s), retry (2-3x exponential backoff), timeout.
- [ ] T-3.3.2 — Rukovanje otvorenim kolom u `PurchaseService`: oslobodi hold, vrati `Result.Failure(Error.Failure("payment.unavailable", "Plaćanje trenutno nije dostupno"))` → `503`.
- [ ] T-3.3.3 — Ručni test: `docker stop` na Payment kontejneru, provjeriti ponašanje, `docker start`, provjeriti oporavak — zabilježiti za demonstraciju u Sprintu 5.

**Eksterni servisi:** **NE.**

---

## US-3.4 — RabbitMQ i kompletna kupovina end-to-end

**Kao** kupac, **želim** da nakon uspješne kupovine sistem asinhrono obradi sporedne zadatke, **kako bih** dobio brz odgovor bez čekanja.

**Acceptance criteria:**
- `POST /purchases` (`holdId`, podaci za plaćanje): poziva `Payment`, na uspjeh upisuje `Ticket` (`Confirmed`), objavljuje `TicketPurchased` na RabbitMQ.
- Objava eventa ne usporava sinhroni odgovor korisniku.

**Taskovi:**
- [ ] T-3.4.1 — Finalizovati `TicketPurchased` u `eTicketing.Contracts` (`TicketId`, `EventId`, `SectorId`, `UserId`, `UserEmail`, `PurchasedAt`).
- [ ] T-3.4.2 — `RabbitMQ.Client`/`MassTransit` u `eTicketing.Ticketing`, deklarisati topologiju (`eticketing.events` exchange).
- [ ] T-3.4.3 — `PurchaseService.PurchaseAsync` — orkestrira hold potvrdu → Payment poziv → `Ticket` insert → publish eventa, sve kroz `Result<PurchaseResponse>`.
- [ ] T-3.4.4 — `Endpoints/PurchaseEndpoints.cs`: `POST /purchases`, `RequireAuthorization()`.
- [ ] T-3.4.5 — End-to-end test bez UI-a (Postman): hold → purchase → provjera `Ticket` zapisa i objavljenog eventa (RabbitMQ management UI).

**Eksterni servisi:** **NE.**

---

## US-3.5 — SuperAdmin/Admin override nad ulaznicama

**Kao** SuperAdmin/Admin, **želim** uvid u sve ulaznice na platformi sa mogućnošću izmjene/brisanja, **kako bih** mogao administrativno rješavati sporne slučajeve.

**Acceptance criteria:**
- `GET /tickets/all` — sve ulaznice, svih korisnika, svih eventa.
- `PUT /tickets/{id}` — izmjena statusa (npr. otkazana/refundirana).
- `DELETE /tickets/{id}` — administrativno brisanje/otkazivanje.
- `GET /tickets/mine` — kupac vidi samo svoje.

**Taskovi:**
- [ ] T-3.5.1 — `TicketService.GetAllAsync` (`PagedResult<TicketResponse>`, `PlatformStaff` samo), `GetMineAsync` (`User`, filtrirano po `UserId` iz JWT-a).
- [ ] T-3.5.2 — `UpdateStatusAsync`, `DeleteAsync` — `PlatformStaff` samo.
- [ ] T-3.5.3 — `Endpoints/TicketEndpoints.cs` sa odgovarajućim autorizacionim politikama.

**Eksterni servisi:** **NE.**

---

## US-3.6 — Angular repoint: javne stranice i auth

**Kao** posjetilac, **želim** da postojeće javne Angular stranice i dalje rade nakon prelaska na novi backend, **kako** promjena arhitekture ne bi prekinula postojeću funkcionalnost.

**Acceptance criteria:**
- `login`/`register` u Angular-u rade protiv `eTicketing.Identity` (kroz Gateway).
- `home`, `events`, `search` rade protiv `eTicketing.Catalog` (kroz Gateway), prikazuju samo `Published` evente.

**Taskovi:**
- [ ] T-3.6.1 — Ažurirati Angular `environment.ts` da pokazuje na novi Gateway URL.
- [ ] T-3.6.2 — Ažurirati `AuthService` (Angular) — novi oblik `LoginResponse`/JWT claim-ova ako se razlikuje od starog.
- [ ] T-3.6.3 — Ažurirati `EventService` (Angular) — novi endpoint oblik (`/api/events`, `PagedResult` shape iz `eTicketing.Catalog`).
- [ ] T-3.6.4 — Regresioni test: registracija, login, pregled eventa kroz Angular protiv novog backenda.

**Eksterni servisi:** **NE.**

---

### Definition of done za Sprint 3

Kompletan sinhroni tok kupovine radi end-to-end (Postman). Circuit breaker demonstrira graceful degradaciju pri padu Payment servisa. SuperAdmin/Admin imaju override nad ulaznicama. Angular javne stranice i auth rade protiv novog backenda kroz Gateway.
