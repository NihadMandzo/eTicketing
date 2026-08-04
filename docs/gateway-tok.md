# Detaljan tok: API Gateway ↔ Frontend ↔ Servisi (potpuni rewrite)

> Prati [arhitektura-migracija-mikroservisi-eda.md](arhitektura-migracija-mikroservisi-eda.md). Cijeli backend je nov — ovaj dokument opisuje finalni, ciljni tok.

## 0. Klijenti i princip izolacije

Dva klijenta, oba isključivo kroz Gateway: `frontend/web` (Angular, customer) i `frontend/desktop` (Flutter, Admin/Organizator). U `docker-compose.yml` **samo** `eTicketing.Gateway` ima port publikovan ka hostu; svi ostali servisi su dostupni isključivo unutar internog Docker network-a.

`eTicketing.Payment` **nema nijednu javnu rutu kroz gateway** — dostupan je isključivo servisu `eTicketing.Ticketing`, interno.

```
                         Host mašina (izloženo javno)
                                    │  http://localhost:5000
                                    ▼
                    ┌───────────────────────────────┐
                    │   eTicketing.Gateway (YARP)    │
                    │   JWT autentikacija, routing    │
                    └───────────────┬─────────────────┘
                                     │  interni Docker network
        ┌────────────────┬──────────┼───────────┬─────────────────┐
        │                 │          │            │                 │
 ┌──────▼─────┐   ┌───────▼──────┐  ┌▼───────────┐ │        (Payment nije
 │  identity   │   │   catalog     │  │ ticketing   │ │         rutiran kroz
 │  (interno)  │   │  (interno)    │  │ (interno)   │ │         gateway)
 └──────┬──────┘   └───────┬───────┘  └─────┬───────┘ │
        │                  ▲                 │         │
        │        (2) interni poziv           │         ▼
        │        Ticketing → Catalog          │   ┌───────────┐
        │        (provjera vlasništva          │   │  payment   │
        │         eventa nad sektorom,          │   │ (SAMO       │
        │         Polly retry/timeout)          │   │  interno)   │
        │                                       │   └───────────┘
        │                                       │  (1) Ticketing → Payment
        │                                       │      (Polly circuit breaker)
        │                                       ▼
        │                              ┌────────────────┐
        │                              │    RabbitMQ     │
        │                              └───────┬──────────┘
        │                          ┌────────────┴────────────┐
        │                          ▼                          ▼
        │                ┌──────────────────┐      ┌──────────────────┐
        └───────────────▶│ notifications-svc │      │ pdf-generation-svc│
        (VerificationEmail│                    │      │                    │
         Requested)       └──────────────────┘      └──────────────────┘
```

## 1. Tabela rutiranja

| Path prefiks (javno, kroz Gateway) | Cilja servis | Zahtijeva JWT? | Rola |
|---|---|---|---|
| `/api/auth/**` | Identity | Ne (register/login javni) | — |
| `/api/organizations/**` | Identity | Da | `SuperAdmin` (write), `Organizer` (`GET` svoje) |
| `/api/admins/**` | Identity | Da | `SuperAdmin` |
| `/api/categories/**` | Catalog | Da (pisanje), Ne (čitanje) | `PlatformStaff` (pisanje) |
| `/api/events/**` | Catalog | Da (pisanje/preview/publish/mine/all), Ne (`GET` liste/pojedinačnog, samo `Published`) | `Organizer` (svoje), `PlatformStaff` (sve) |
| `/api/sectors/**` | Ticketing | Da (pisanje/preview/publish/hold), Ne (čitanje `Published`) | `Organizer` (svoje), `PlatformStaff` (sve) |
| `/api/purchases/**` | Ticketing | Da | `User` |
| `/api/tickets/**` | Ticketing | Da | `User` (svoje), `PlatformStaff` (sve, uklj. edit/delete) |
| — (nema javnu rutu) | Payment | — | samo interno, `Ticketing → Payment` |
| — (nema javnu rutu) | Notifications, PdfGeneration | — | samo RabbitMQ konzumenti |

> **Interne (ne-gateway) rute između servisa:** `Ticketing → Catalog`: `GET /internal/events/{id}` (vraća minimalan DTO: `Id`, `OrganizationId`, `Status`) — koristi se isključivo za provjeru vlasništva pri kreiranju/izmjeni sektora, nikad ne prolazi kroz gateway.

## 2. Tok registracije i logina

```mermaid
sequenceDiagram
    participant FE as Frontend (Angular/Desktop)
    participant GW as Gateway
    participant ID as Identity

    FE->>GW: POST /api/auth/register { email, password, ... }
    GW->>ID: forward (javna ruta)
    ID->>ID: kreiraj User (rola User), pošalji VerificationEmailRequested na RabbitMQ
    ID-->>GW: 201
    GW-->>FE: 201

    FE->>GW: POST /api/auth/login { email, password }
    GW->>ID: forward
    ID->>ID: provjeri kredencijale, izdaj JWT (claims: sub, role, organizationId)
    ID-->>GW: 200 { token }
    GW-->>FE: 200 { token }
```

## 3. SuperAdmin — kreiranje organizacije i organizatora

```mermaid
sequenceDiagram
    participant FE as Desktop app (SuperAdmin)
    participant GW as Gateway
    participant ID as Identity

    FE->>GW: POST /api/organizations { org podaci + prvi organizator } (JWT: role=SuperAdmin)
    GW->>ID: forward
    ID->>ID: transakcija: INSERT Organization + INSERT User (role=OrgSuperAdmin, organizationId=novi)
    ID-->>GW: 201 { organizationId, organizerUserId }
    GW-->>FE: 201

    FE->>GW: POST /api/organizations/{id}/users { podaci + rola } (dodatni organizator)
    GW->>ID: forward
    ID-->>GW: 201
    GW-->>FE: 201
```

## 4. Organizator — kreiranje eventa sa preview/publish obrascem

```mermaid
sequenceDiagram
    participant FE as Desktop app (Organizator)
    participant GW as Gateway
    participant CT as Catalog

    FE->>GW: POST /api/events/preview { naziv, opis, datum, kategorija... } (JWT: organizationId=X)
    GW->>CT: forward
    CT->>CT: validacija BEZ upisa u bazu (Result<PreviewResponse>)
    CT-->>GW: 200 { preview podaci } ili 400 { validacione greške }
    GW-->>FE: prikaži preview korisniku

    Note over FE: organizator potvrđuje

    FE->>GW: POST /api/events { isti podaci }
    GW->>CT: forward
    CT->>CT: INSERT Event (Status=Draft, OrganizationId=X iz JWT)
    CT-->>GW: 201 { eventId }
    GW-->>FE: 201

    FE->>GW: POST /api/events/{id}/publish
    GW->>CT: forward
    CT->>CT: UPDATE Event SET Status=Published (provjera: Event.OrganizationId == JWT.organizationId)
    CT-->>GW: 200
    GW-->>FE: 200
```

Isti obrazac (`preview` → `create/update` → `publish`) ponavlja se identično za `POST /api/sectors/preview` u `Ticketing`, s tom razlikom da `Ticketing` dodatno interno poziva `Catalog` (`GET /internal/events/{id}`) da potvrdi da `EventId` iz zahtjeva pripada organizaciji pozivaoca prije nego dozvoli kreiranje sektora.

## 5. Kompletan tok kupovine

```mermaid
sequenceDiagram
    participant FE as Angular Frontend
    participant GW as Gateway
    participant TS as Ticketing
    participant R as Redis
    participant PS as Payment
    participant MQ as RabbitMQ
    participant NS as Notifications
    participant PDF as PDF Generator

    FE->>GW: POST /api/sectors/5/hold { qty: 2 } (JWT: role=User)
    GW->>TS: forward
    TS->>R: atomarni decrement (Lua), TTL 5min
    alt dovoljno kapaciteta
        R-->>TS: OK, holdId
        TS-->>GW: 200 { holdId, expiresAt }
    else nema kapaciteta
        TS-->>GW: 409 Conflict
    end
    GW-->>FE: forward odgovor

    FE->>GW: POST /api/purchases { holdId, cardInfo }
    GW->>TS: forward
    TS->>PS: POST /payments (Polly circuit breaker)
    alt Payment dostupan i uspješan
        PS-->>TS: 200 { transactionId }
        TS->>TS: INSERT Ticket (Confirmed), potvrdi hold
        TS->>MQ: publish TicketPurchased
        TS-->>GW: 200 { ticketId, status: processing }
        MQ--)NS: consume → pošalji email
        MQ--)PDF: consume → generiši PDF, upload Blob Storage
    else Payment nedostupan / circuit otvoren
        PS--xTS: fail-fast
        TS->>R: oslobodi hold
        TS-->>GW: 503 "Plaćanje trenutno nije dostupno"
    end
    GW-->>FE: forward odgovor
```

## 6. SuperAdmin/Admin override

```mermaid
sequenceDiagram
    participant FE as Desktop app (SuperAdmin/Admin)
    participant GW as Gateway
    participant CT as Catalog
    participant TS as Ticketing

    FE->>GW: GET /api/events/all (JWT: role=SuperAdmin ili Admin)
    GW->>CT: forward
    CT->>CT: vrati SVE evente, svih organizacija, svih statusa (bez ownership filtera)
    CT-->>GW: 200 [...]
    GW-->>FE: 200

    FE->>GW: GET /api/tickets/all
    GW->>TS: forward
    TS-->>GW: 200 [...] (sve ulaznice, svih korisnika)
    GW-->>FE: 200

    FE->>GW: DELETE /api/tickets/{id}
    GW->>TS: forward
    TS->>TS: obriši/otkaži ulaznicu (administrativni override)
    TS-->>GW: 204
    GW-->>FE: 204
```
