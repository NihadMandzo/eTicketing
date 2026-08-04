# SPRINT 1 (Dan 1–5) — Infrastruktura + eTicketing.Identity

**Cilj sprinta:** postaviti infrastrukturu i standard za sve buduće servise, i isporučiti `eTicketing.Identity` — auth, organizacije, admin nalozi. Ovo je temelj na kojem stoje svi ostali servisi (JWT izdavanje, role, organizacije).

> Obrasci (Result, Repository, PagedResult, Preview) definisani su u [backend-projekt-template.md](../docs/backend-projekt-template.md). Tačne komande za kreiranje projekata (3-slojna arhitektura: `.Api`/`.Business`/`.Data`), `dotnet add reference` wiring i Docker povezivanje: [backend-setup-guide.md](../docs/backend-setup-guide.md) — svaki servis u ovom i narednim sprintovima prati taj vodič.

---

## US-1.1 — Docker infrastruktura i projektni skeleton

**Kao** developer, **želim** docker-compose okruženje i standardizovan skeleton projekta, **kako bih** svaki naredni servis gradio po istom obrascu bez ponovnog izmišljanja strukture.

**Acceptance criteria:**
- `docker-compose up` pokreće SQL Server, RabbitMQ (sa management UI), Redis.
- `eTicketing.Contracts` projekat postoji sa `Results/`, `Pagination/`, `Events/` folderima (vidi template).
- Jasno je zapisana odluka šta se dešava sa starim monolitom tokom razvoja.

**Taskovi:**
- [ ] T-1.1.1 — Kreirati/proširiti `docker-compose.yml`: SQL Server (jedna instanca, baza po servisu), RabbitMQ, Redis, zajednički Docker network.
- [ ] T-1.1.2 — Kreirati `eTicketing.sln` i `eTicketing.Contracts` (class library): `Results/` (`Error`, `Result`, `Result<T>`, `ResultExtensions`), `Pagination/` (`PagedResult<T>`, `QueryableExtensions`), `Persistence/` (`IRepository<T>`, `Repository<T>`, `IUnitOfWork`), `Events/` (skeletoni: `TicketPurchased`, `VerificationEmailRequested`, `PaymentFailed`) — tačne komande u [backend-setup-guide.md](../docs/backend-setup-guide.md), sekcija 2.
- [ ] T-1.1.3 — Kreirati novi `backend/services/`, `backend/gateway/`, `backend/shared/` folder raspored (odvojeno od starog `backend/eTicketing.Api` i dr.) — **stari monolit ostaje u repozitoriju nedirnut**, koristi se samo kao referenca za kopiranje validacionih atributa/DTO oblika (npr. `OrganizationInsertRequest`, `RoleType` enum), briše se na kraju u Sprint 5 (US-5.3).
- [ ] T-1.1.4 — Scaffold `eTicketing.Identity` kao tri projekta (`eTicketing.Identity.Api`, `.Business`, `.Data`) po [backend-setup-guide.md](../docs/backend-setup-guide.md), sekcija 3, sa `ProjectReference` wiring-om na `eTicketing.Contracts`.
- [ ] T-1.1.5 — `Program.cs` (u `.Api` projektu) sa DI skeletonom, `/health` endpoint, `appsettings.json` sa connection stringom za `IdentityDb`.

**Eksterni servisi:** **NE.** Sve self-hosted u kontejnerima.

---

## US-1.2 — Registracija i login

**Kao** korisnik (bilo koje uloge), **želim** da se registrujem i prijavim, **kako bih** dobio pristupni token za korištenje platforme.

**Acceptance criteria:**
- `POST /auth/register` kreira korisnika sa rolom `User` (samoregistracija — organizatori i admini se kreiraju samo preko SuperAdmin tokova iz US-1.3/1.4/1.5, ne kroz javnu registraciju).
- `POST /auth/login` vraća JWT sa `sub` (userId), `role`, `organizationId` (null osim za organizatore) claim-ovima.
- Lozinke se hashuju (ne čuvaju u plain textu) — kopirati/adaptirati postojeći `PasswordHelper` obrazac iz starog monolita.
- Neispravni kredencijali vraćaju `Result.Failure` sa `ErrorType.Validation`/`Unauthorized`, ne exception.

**Taskovi:**
- [ ] T-1.2.1 — `Domain/Entities/User.cs`, `Role.cs`; `Infrastructure/Persistence/IdentityDbContext.cs`; prva EF Core migracija.
- [ ] T-1.2.2 — Role seed: `SuperAdmin(1)`, `Admin(2)`, `OrganizationSuperAdmin(3)`, `OrganizationAdmin(4)`, `User(5)` (isti oblik kao postojeći `RoleSeed.cs` u starom monolitu — kopirati/adaptirati).
- [ ] T-1.2.3 — `POST /auth/register` — `Application/Auth/RegisterService.cs`, hashovanje lozinke, `Result<UserResponse>`.
- [ ] T-1.2.4 — `POST /auth/login` — provjera kredencijala, generisanje JWT-a (claims: `sub`, `role`, `organizationId`), `Result<LoginResponse>`.
- [ ] T-1.2.5 — Nakon uspješne registracije, objaviti `VerificationEmailRequested` na RabbitMQ (konzumira se tek u Sprintu 4 kad `Notifications` postoji — za sada samo publish, bez greške ako nema consumera).
- [ ] T-1.2.6 — Endpoint `Endpoints/AuthEndpoints.cs` (`MapGroup("/auth")`), oba endpointa `AllowAnonymous`.

**Eksterni servisi:** **NE** (RabbitMQ self-hosted; stvarno slanje emaila dolazi tek u Sprintu 4 sa `Notifications` servisom).

---

## US-1.3 — SuperAdmin kreira organizaciju i prvog organizatora

**Kao** SuperAdmin, **želim** kreirati novu organizaciju i njen prvi pristupni nalog (organizatora) u jednom koraku, **kako bi** organizacija odmah mogla početi raditi.

**Acceptance criteria:**
- `POST /organizations` prima podatke organizacije **i** podatke prvog organizatora (ime, prezime, email, username, lozinka, rola `OrganizationSuperAdmin` ili `OrganizationAdmin`) u jednom zahtjevu.
- Organizacija i korisnik se kreiraju u jednoj transakciji (ako jedno ne uspije, ne uspije ni drugo).
- Endpoint dostupan **isključivo** roli `SuperAdmin`.
- Novi organizator se odmah može prijaviti i dobija JWT sa `organizationId` postavljenim na novu organizaciju.

**Taskovi:**
- [ ] T-1.3.1 — `Domain/Entities/Organization.cs` (Name, Description, Address, Phone, Email, Website, IsActive). EF Core migracija.
- [ ] T-1.3.2 — `CreateOrganizationRequest` (org polja + `AdminFirstName/LastName/Email/Username/Password/RoleId` — isti oblik kao postojeći `OrganizationInsertRequest` iz starog monolita, adaptiran).
- [ ] T-1.3.3 — `OrganizationService.CreateAsync` — transakciono kreira `Organization` + `User` (rola prema `RoleId`, ograničeno na `OrganizationSuperAdmin`/`OrganizationAdmin`), koristi `IUnitOfWork.SaveChangesAsync` jednom na kraju.
- [ ] T-1.3.4 — Autorizaciona politika `PlatformStaff` (`SuperAdmin`, `Admin`) i `SuperAdminOnly` — `POST /organizations` zaštićen sa `SuperAdminOnly` (kreiranje organizacije je isključivo SuperAdmin ovlaštenje, ne i Admin).
- [ ] T-1.3.5 — `PUT /organizations/{id}`, `DELETE /organizations/{id}` — `PlatformStaff`.
- [ ] T-1.3.6 — `GET /organizations`, `GET /organizations/{id}` — javno čitanje (Angular web treba listu organizacija/eventa javno).

**Eksterni servisi:** **NE.**

---

## US-1.4 — SuperAdmin kreira Admin naloge

**Kao** SuperAdmin, **želim** kreirati naloge za platform Admin osoblje, **kako bi** imao pomoć u administraciji platforme bez davanja pune SuperAdmin ovlasti.

**Acceptance criteria:**
- `POST /admins` kreira korisnika sa rolom `Admin` (bez `organizationId`).
- Endpoint dostupan **isključivo** roli `SuperAdmin` (ne i `Admin` — Admin ne smije kreirati druge Admin naloge, eksplicitan zahtjev).
- `GET /admins`, `DELETE /admins/{id}` — isto ograničeno na `SuperAdmin`.

**Taskovi:**
- [ ] T-1.4.1 — `CreateAdminRequest` (ime, prezime, email, username, lozinka).
- [ ] T-1.4.2 — `AdminService.CreateAsync/GetAsync/DeleteAsync`, koristi isti `PagedResult<T>` obrazac za `GET /admins`.
- [ ] T-1.4.3 — `Endpoints/AdminEndpoints.cs` (`MapGroup("/admins")`), sve akcije `RequireAuthorization("SuperAdminOnly")`.

**Eksterni servisi:** **NE.**

---

## US-1.5 — SuperAdmin dodaje dodatnog organizatora postojećoj organizaciji

**Kao** SuperAdmin, **želim** dodati dodatni pristupni nalog postojećoj organizaciji, **kako** organizacija ne bi zavisila samo o jednom nalogu.

**Acceptance criteria:**
- `POST /organizations/{id}/users` kreira korisnika vezanog za datu organizaciju, rola `OrganizationSuperAdmin` ili `OrganizationAdmin`.
- `GET /organizations/{id}/users` — vidljivo `SuperAdmin`-u (sve organizacije) i organizatoru te iste organizacije (samo svoja).
- `DELETE /organizations/{id}/users/{userId}` — `SuperAdmin`.

**Taskovi:**
- [ ] T-1.5.1 — `AddOrganizationUserRequest` (isti oblik kao stari `OrganizationUserRequest`, `RoleId` ograničen na 3/4).
- [ ] T-1.5.2 — `OrganizationService.AddUserAsync/RemoveUserAsync/GetUsersAsync` (`GetUsersAsync` vraća `PagedResult<UserResponse>`).
- [ ] T-1.5.3 — Autorizacija: `POST`/`DELETE` — `SuperAdminOnly`; `GET` — `PlatformStaff` (sve) ili `Organizer` uz provjeru da je `{id}` == `organizationId` iz JWT-a.

**Eksterni servisi:** **NE.**

---

## US-1.6 — Globalni error handling i Result pattern kroz cijeli servis

**Kao** developer, **želim** da svaka greška u `eTicketing.Identity` prođe kroz konzistentan Result pattern ili globalni exception handler, **kako bi** klijenti (Angular, Desktop) dobijali predvidive JSON odgovore o greškama.

**Acceptance criteria:**
- Sve poslovne greške (nevažeći podaci, email već zauzet, nije pronađeno) vraćaju `Result.Failure` mapiran u odgovarajući HTTP status (`ResultExtensions.ToHttpResult`).
- Neočekivane greške (exception) hvata `GlobalExceptionHandler`, vraća konzistentan JSON, loguje stack trace.
- `/health` endpoint provjerava konekciju na `IdentityDb`.

**Taskovi:**
- [ ] T-1.6.1 — Implementirati `Middleware/GlobalExceptionHandler.cs` po uzoru na [backend-projekt-template.md](../docs/backend-projekt-template.md) sekcija 5.
- [ ] T-1.6.2 — Proći kroz sve endpointe iz US-1.2 – 1.5 i potvrditi da svi koriste `Result<T>`/`Result` umjesto bacanja exception-a za poslovne greške.
- [ ] T-1.6.3 — Testirati: registracija sa zauzetim emailom → `400` sa jasnom porukom (ne `500`); nepostojeća organizacija → `404`.
- [ ] T-1.6.4 — Ručni regresioni test cijelog Sprint 1 toka: register → login → SuperAdmin kreira org+organizator → organizator login → SuperAdmin kreira Admin nalog → SuperAdmin dodaje drugog organizatora.

**Eksterni servisi:** **NE.**

---

### Definition of done za Sprint 1

`eTicketing.Identity` radi samostalno (svoj kontejner, svoja baza `IdentityDb`). Registracija/login rade za sve uloge. SuperAdmin kreira organizacije (sa prvim organizatorom), dodaje dodatne organizatore, kreira Admin naloge — sve ostalo (Admin, organizatori) ograničeno tačno prema zahtjevima. JWT nosi ispravne claim-ove za sve buduće servise.
