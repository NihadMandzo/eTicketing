# Backend setup vodič — kreiranje Gateway-a i svih mikroservisa (3-slojna arhitektura)

> Operativni, korak-po-korak vodič: kako fizički kreirati projekte, kako se referenciraju, kako se povezuju sa Docker-om. Konceptualna pozadina (Result Pattern, Repository, PagedResult, Preview obrazac) je u [backend-projekt-template.md](backend-projekt-template.md) — ovaj dokument pokazuje **gdje tačno** taj kod fizički živi kad se servis podijeli na tri odvojena projekta: **`.Api`**, **`.Business`**, **`.Data`**.
>
> Prati [arhitektura-migracija-mikroservisi-eda.md](arhitektura-migracija-mikroservisi-eda.md) — cijeli backend se piše ispočetka. Pretpostavke: instaliran **.NET 10 SDK** (najnovija stabilna verzija — projekat koristi `net10.0` kao ciljni framework, pinovano u `backend/global.json`), **Docker Desktop**, `dotnet-ef` alat (`dotnet tool install --global dotnet-ef`).

---

## 1. Struktura repozitorija

Novi kod ide **pored** starog monolita (koji ostaje netaknut do brisanja u Sprint 5, T-5.3.4 — vidi [SPRINT_5.md](../SPRINTS/SPRINT_5.md)):

```
eTicketing/
└── backend/
    ├── eTicketing.Api/            ← STARI monolit, privremeno, ne dirati
    ├── eTicketing.Services/       ← STARI
    ├── eTicketing.Model/          ← STARI
    ├── eTicketing.EmailWorker/    ← STARI
    ├── backend.sln                ← STARI solution
    │
    ├── eTicketing.sln              ← NOVI glavni solution — sve što se pravi od sada
    ├── shared/
    │   └── eTicketing.Contracts/          (Result, PagedResult, Repository generics, event ugovori)
    ├── gateway/
    │   └── eTicketing.Gateway/            (YARP, jedan projekat, bez 3 sloja — vidi sekciju 6)
    └── services/
        ├── identity/
        │   ├── eTicketing.Identity.Api/
        │   ├── eTicketing.Identity.Business/
        │   └── eTicketing.Identity.Data/
        ├── catalog/        (isti obrazac: .Api / .Business / .Data)
        ├── ticketing/       (isti obrazac)
        ├── payment/         (isti obrazac)
        ├── notifications/
        │   └── eTicketing.Notifications/  (JEDAN projekat — vidi izuzetak u sekciji 7)
        └── pdfgeneration/
            └── eTicketing.PdfGeneration/  (JEDAN projekat — isti izuzetak)
```

**Zašto svaki servis ima tačno tri projekta:**

| Projekat | SDK tip | Sadržaj | Referencira |
|---|---|---|---|
| `.Data` (DAL) | `Microsoft.NET.Sdk` (class library) | Entiteti, `DbContext`, EF konfiguracije, migracije, Repository (interfejs + implementacija), Redis pristup | `eTicketing.Contracts` |
| `.Business` (BLL) | `Microsoft.NET.Sdk` (class library) | Servisi (poslovna logika), Request/Response DTO-i, validacija, `Result<T>` korištenje | `.Data`, `eTicketing.Contracts` |
| `.Api` | `Microsoft.NET.Sdk.Web` | `Program.cs` (composition root), Minimal API endpoint-i, middleware, `appsettings.json` | `.Business`, `.Data`, `eTicketing.Contracts` |

Ovo je **klasična slojevita (N-tier) arhitektura**, zavisnost ide u jednom smjeru: `Api → Business → Data`. `Api` dodatno direktno referencira `.Data` samo zato što `Program.cs` (composition root) mora vidjeti konkretne tipove (`TicketingDbContext`, `SectorRepository`) da bi ih registrovao u DI kontejner — to nije kršenje slojevanja, to je uobičajena uloga composition root-a.

---

## 2. Korak 1 — `eTicketing.sln` i `eTicketing.Contracts`

```bash
cd backend
dotnet new sln -n eTicketing

dotnet new classlib -n eTicketing.Contracts -o shared/eTicketing.Contracts
dotnet sln eTicketing.sln add shared/eTicketing.Contracts/eTicketing.Contracts.csproj
```

**NuGet paketi u `eTicketing.Contracts`:**

```bash
cd shared/eTicketing.Contracts
dotnet add package Microsoft.EntityFrameworkCore
```

> **Pragmatična odluka:** `Contracts` sadrži i EF-Core-zavisne dijelove (`IRepository<T>`, `QueryableExtensions.ToPagedResultAsync`) i ASP.NET-Core-zavisne dijelove (`ResultExtensions.ToHttpResult`) u **jednom** projektu, iako "čisto" rješenje bi to razdvojilo u 2-3 manja paketa. Za obim od 31 dana i solo razvoj, ta dodatna granularnost je čist trošak bez koristi — `Notifications`/`PdfGeneration` (koji ne trebaju EF Core ni ASP.NET Core tipove) jednostavno neće te klase koristiti, referenca im ne šteti (par MB neiskorištenih DLL-ova, nula runtime troška).

Za ASP.NET Core tipove (`IResult`, `Results`) u `ResultExtensions`, dodati u `eTicketing.Contracts.csproj`:

```xml
<ItemGroup>
  <FrameworkReference Include="Microsoft.AspNetCore.App" />
</ItemGroup>
```

**Sadržaj `eTicketing.Contracts` (puna specifikacija koda: [backend-projekt-template.md](backend-projekt-template.md), sekcije 5-7):**

```
eTicketing.Contracts/
├── Results/
│   ├── Error.cs
│   ├── Result.cs
│   └── ResultExtensions.cs
├── Pagination/
│   ├── PagedResult.cs
│   └── QueryableExtensions.cs       (ToPagedResultAsync — EF Core)
├── Persistence/
│   ├── IRepository.cs
│   ├── Repository.cs                (generička EF Core implementacija)
│   └── IUnitOfWork.cs
└── Events/
    ├── TicketPurchased.cs
    ├── VerificationEmailRequested.cs
    └── PaymentFailed.cs
```

`IRepository<T>`/`Repository<T>` u `Contracts` su **generički** (rade nad bilo kojim `DbContext`-om), tako da svaki servis samo naslijedi/registruje `Repository<EventSector>` bez pisanja iste generičke klase 7 puta.

---

## 3. Korak 2 — jedan servis, na primjeru `eTicketing.Ticketing`

### 3.1. Kreiranje tri projekta

```bash
cd backend/services/ticketing

dotnet new classlib -n eTicketing.Ticketing.Data
dotnet new classlib -n eTicketing.Ticketing.Business
dotnet new web -n eTicketing.Ticketing.Api

cd ../../
dotnet sln eTicketing.sln add services/ticketing/eTicketing.Ticketing.Data/eTicketing.Ticketing.Data.csproj
dotnet sln eTicketing.sln add services/ticketing/eTicketing.Ticketing.Business/eTicketing.Ticketing.Business.csproj
dotnet sln eTicketing.sln add services/ticketing/eTicketing.Ticketing.Api/eTicketing.Ticketing.Api.csproj
```

### 3.2. Reference između projekata

```bash
cd services/ticketing

dotnet add eTicketing.Ticketing.Data/eTicketing.Ticketing.Data.csproj reference ../../shared/eTicketing.Contracts/eTicketing.Contracts.csproj

dotnet add eTicketing.Ticketing.Business/eTicketing.Ticketing.Business.csproj reference eTicketing.Ticketing.Data/eTicketing.Ticketing.Data.csproj
dotnet add eTicketing.Ticketing.Business/eTicketing.Ticketing.Business.csproj reference ../../shared/eTicketing.Contracts/eTicketing.Contracts.csproj

dotnet add eTicketing.Ticketing.Api/eTicketing.Ticketing.Api.csproj reference eTicketing.Ticketing.Business/eTicketing.Ticketing.Business.csproj
dotnet add eTicketing.Ticketing.Api/eTicketing.Ticketing.Api.csproj reference eTicketing.Ticketing.Data/eTicketing.Ticketing.Data.csproj
dotnet add eTicketing.Ticketing.Api/eTicketing.Ticketing.Api.csproj reference ../../shared/eTicketing.Contracts/eTicketing.Contracts.csproj
```

Rezultat (`dotnet list reference` u `.Api`): `Business`, `Data`, `Contracts`. U `.Business`: `Data`, `Contracts`. U `.Data`: `Contracts`.

### 3.3. NuGet paketi po projektu

```bash
# .Data — pristup bazi i Redisu
cd eTicketing.Ticketing.Data
dotnet add package Microsoft.EntityFrameworkCore.SqlServer
dotnet add package Microsoft.EntityFrameworkCore.Design
dotnet add package StackExchange.Redis

# .Business — LINQ async (IQueryable kompozicija nad Data slojem)
cd ../eTicketing.Ticketing.Business
dotnet add package Microsoft.EntityFrameworkCore

# .Api — web host, auth, resiliency, health checks
cd ../eTicketing.Ticketing.Api
dotnet add package Microsoft.AspNetCore.Authentication.JwtBearer
dotnet add package Microsoft.Extensions.Http.Resilience
dotnet add package Microsoft.EntityFrameworkCore.Design
dotnet add package Scalar.AspNetCore
```

### 3.4. Sadržaj `.Data` sloja

```
eTicketing.Ticketing.Data/
├── Entities/
│   ├── EventSector.cs
│   └── Ticket.cs
├── TicketingDbContext.cs
├── Configurations/
│   ├── EventSectorConfiguration.cs
│   └── TicketConfiguration.cs
├── Repositories/
│   ├── ISectorRepository.cs
│   └── SectorRepository.cs          (nasljeđuje Repository<EventSector> iz Contracts)
├── Redis/
│   ├── ISectorCapacityLock.cs
│   └── RedisSectorCapacityLock.cs
└── Migrations/                       (generisano preko dotnet ef)
```

```csharp
// TicketingDbContext.cs
public class TicketingDbContext : DbContext, IUnitOfWork
{
    public TicketingDbContext(DbContextOptions<TicketingDbContext> options) : base(options) { }

    public DbSet<EventSector> EventSectors => Set<EventSector>();
    public DbSet<Ticket> Tickets => Set<Ticket>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(TicketingDbContext).Assembly);
    }
}
```

```csharp
// Repositories/SectorRepository.cs
public interface ISectorRepository : IRepository<EventSector>
{
    Task<List<EventSector>> GetByEventIdAsync(int eventId, CancellationToken ct = default);
}

public class SectorRepository : Repository<EventSector>, ISectorRepository
{
    public SectorRepository(TicketingDbContext context) : base(context) { }

    public Task<List<EventSector>> GetByEventIdAsync(int eventId, CancellationToken ct = default)
        => Query().Where(s => s.EventId == eventId).ToListAsync(ct);
}
```

(`Repository<T>` generička bazna klasa dolazi iz `eTicketing.Contracts.Persistence` — vidi sekciju 2.)

### 3.5. Sadržaj `.Business` sloja

```
eTicketing.Ticketing.Business/
├── Sectors/
│   ├── ISectorService.cs
│   ├── SectorService.cs
│   ├── UpsertSectorRequest.cs
│   ├── SectorResponse.cs
│   ├── SectorPreviewResponse.cs
│   └── SectorQuery.cs
├── Tickets/
│   ├── ITicketService.cs
│   └── TicketService.cs
└── External/
    └── ICatalogClient.cs             (interfejs za interni poziv ka Catalog servisu — implementacija/HttpClient wiring ostaje u .Api, jer HttpClientFactory i Polly politike su infrastrukturna/hosting briga)
```

```csharp
// Sectors/SectorService.cs
public class SectorService : ISectorService
{
    private readonly ISectorRepository _repository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ISectorCapacityLock _capacityLock;
    private readonly ICatalogClient _catalogClient;

    public SectorService(ISectorRepository repository, IUnitOfWork unitOfWork,
        ISectorCapacityLock capacityLock, ICatalogClient catalogClient)
    {
        _repository = repository;
        _unitOfWork = unitOfWork;
        _capacityLock = capacityLock;
        _catalogClient = catalogClient;
    }

    public async Task<Result<SectorResponse>> CreateAsync(
        UpsertSectorRequest request, ClaimsPrincipal user, CancellationToken ct)
    {
        var ownership = await ValidateEventOwnershipAsync(request.EventId, user, ct);
        if (ownership.IsFailure) return Result<SectorResponse>.Failure(ownership.Error);

        var entity = new EventSector
        {
            EventId = request.EventId,
            Name = request.Name,
            Capacity = request.Capacity,
            AvailableCount = request.Capacity,
            Price = request.Price,
            Status = PublishStatus.Draft
        };

        await _repository.AddAsync(entity, ct);
        await _unitOfWork.SaveChangesAsync(ct);

        return Result<SectorResponse>.Success(SectorResponse.From(entity));
    }

    private async Task<Result> ValidateEventOwnershipAsync(
        int eventId, ClaimsPrincipal user, CancellationToken ct)
    {
        if (user.IsPlatformStaff()) return Result.Success();

        var eventInfo = await _catalogClient.GetEventAsync(eventId, ct);
        if (eventInfo is null)
            return Result.Failure(Error.NotFound("event.not_found", "Event nije pronađen."));

        return eventInfo.OrganizationId == user.GetOrganizationId()
            ? Result.Success()
            : Result.Failure(Error.Unauthorized("sector.forbidden", "Event ne pripada vašoj organizaciji."));
    }
}
```

### 3.6. Sadržaj `.Api` sloja (composition root)

```
eTicketing.Ticketing.Api/
├── Program.cs
├── Endpoints/
│   ├── SectorEndpoints.cs
│   └── TicketEndpoints.cs
├── Middleware/
│   └── GlobalExceptionHandler.cs
├── Infrastructure/
│   └── HttpCatalogClient.cs          (implementacija ICatalogClient iz Business — HttpClientFactory + Polly ovdje žive)
└── appsettings.json
```

```csharp
// Program.cs
var builder = WebApplication.CreateBuilder(args);

// --- Data sloj ---
builder.Services.AddDbContext<TicketingDbContext>(opt =>
    opt.UseSqlServer(builder.Configuration.GetConnectionString("TicketingDb")));
builder.Services.AddScoped<IUnitOfWork>(sp => sp.GetRequiredService<TicketingDbContext>());
builder.Services.AddScoped<ISectorRepository, SectorRepository>();
builder.Services.AddSingleton<IConnectionMultiplexer>(
    ConnectionMultiplexer.Connect(builder.Configuration.GetConnectionString("Redis")!));
builder.Services.AddScoped<ISectorCapacityLock, RedisSectorCapacityLock>();

// --- Business sloj ---
builder.Services.AddScoped<ISectorService, SectorService>();
builder.Services.AddScoped<ITicketService, TicketService>();

// --- interni poziv ka Catalog servisu (Polly circuit breaker + retry) ---
builder.Services.AddHttpClient<ICatalogClient, HttpCatalogClient>(client =>
    {
        client.BaseAddress = new Uri(builder.Configuration["Services:Catalog"]!);
    })
    .AddResilienceHandler("catalog-pipeline", pb =>
    {
        pb.AddRetry(new HttpRetryStrategyOptions { MaxRetryAttempts = 3 });
        pb.AddTimeout(TimeSpan.FromSeconds(5));
    });

// --- interni poziv ka Payment servisu (Polly circuit breaker + retry + timeout) ---
builder.Services.AddHttpClient<IPaymentClient, HttpPaymentClient>(client =>
    {
        client.BaseAddress = new Uri(builder.Configuration["Services:Payment"]!);
    })
    .AddResilienceHandler("payment-pipeline", pb =>
    {
        pb.AddRetry(new HttpRetryStrategyOptions { MaxRetryAttempts = 2 });
        pb.AddCircuitBreaker(new HttpCircuitBreakerStrategyOptions
        {
            FailureRatio = 0.5, SamplingDuration = TimeSpan.FromSeconds(30),
            BreakDuration = TimeSpan.FromSeconds(15)
        });
        pb.AddTimeout(TimeSpan.FromSeconds(5));
    });

// --- Auth ---
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(opt => { /* isti signing key kao Identity/Gateway, iz appsettings/env */ });
builder.Services.AddAuthorizationBuilder()
    .AddPolicy("Organizer", p => p.RequireRole("OrganizationSuperAdmin", "OrganizationAdmin", "Admin", "SuperAdmin"))
    .AddPolicy("PlatformStaff", p => p.RequireRole("Admin", "SuperAdmin"));

// --- Middleware, health, docs ---
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.AddProblemDetails();
builder.Services.AddHealthChecks().AddDbContextCheck<TicketingDbContext>("database");

var app = builder.Build();

app.UseExceptionHandler();
app.UseAuthentication();
app.UseAuthorization();

app.MapSectorEndpoints();
app.MapTicketEndpoints();
app.MapHealthChecks("/health");

app.Run();
```

Ovo je mjesto gdje se vidi **zašto `.Api` referencira i `.Business` i `.Data` direktno** — bez toga `Program.cs` ne bi imao vidljivost nad `TicketingDbContext`, `SectorRepository`, `RedisSectorCapacityLock` da ih registruje.

---

## 4. Migracije (EF Core CLI)

`DbContext` je u `.Data` projektu, ali taj projekat nema izvršni entry point ni konfiguraciju (connection string) — zato EF CLI treba **startup projekat** (`.Api`):

```bash
cd backend/services/ticketing

dotnet ef migrations add InitialCreate \
  --project eTicketing.Ticketing.Data \
  --startup-project eTicketing.Ticketing.Api \
  --output-dir Migrations

dotnet ef database update \
  --project eTicketing.Ticketing.Data \
  --startup-project eTicketing.Ticketing.Api
```

(Automatska migracija na startu — `dbContext.Database.MigrateAsync()` u `Program.cs`, isti obrazac kao stari monolit.)

---

## 5. Ponoviti za ostale servise

Isti recept (sekcije 3-4) se ponavlja za `Identity`, `Catalog`, `Payment`. Specifičnosti po servisu:

| Servis | Dodatni NuGet paketi (`.Data`) | Napomena |
|---|---|---|
| `Identity` | — | Izdaje JWT (`System.IdentityModel.Tokens.Jwt` u `.Api`, generisanje tokena može biti u `.Business` ili `.Api` — preporuka: `.Business` jer je poslovno pravilo "šta ide u token", `.Api` samo poziva) |
| `Catalog` | `Azure.Storage.Blobs` (upload slike eventa) | Izlaže `/internal/events/{id}` za `Ticketing` |
| `Ticketing` | `StackExchange.Redis`, `RabbitMQ.Client` (ili `MassTransit`) | Prikazano u sekciji 3 |
| `Payment` | — (ili `Stripe.net` ako se ide na Stripe test mode) | **Nema publikovan port u docker-compose** — samo interni servis |

---

## 6. Gateway (`eTicketing.Gateway`) — poseban slučaj, samo jedan projekat

Gateway **nema** 3 sloja — to je čist routing proxy, nema poslovne logike ni pristupa bazi.

```bash
cd backend/gateway
dotnet new web -n eTicketing.Gateway
cd ../
dotnet sln ../eTicketing.sln add gateway/eTicketing.Gateway/eTicketing.Gateway.csproj

cd gateway/eTicketing.Gateway
dotnet add package Yarp.ReverseProxy
dotnet add package Microsoft.AspNetCore.Authentication.JwtBearer
```

```
eTicketing.Gateway/
├── Program.cs
└── appsettings.json      (ReverseProxy:Routes i Clusters — vidi gateway-tok.md sekcija 1)
```

```csharp
// Program.cs
var builder = WebApplication.CreateBuilder(args);

builder.Services.AddReverseProxy()
    .LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"));

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(opt => { /* isti signing key kao Identity */ });
builder.Services.AddAuthorization();

builder.Services.AddCors(opt => opt.AddDefaultPolicy(p =>
    p.WithOrigins("http://localhost:4200" /* Angular */, "http://localhost:XXXX" /* Desktop, ako primjenjivo */)
     .AllowAnyHeader().AllowAnyMethod()));

var app = builder.Build();

app.UseCors();
app.UseAuthentication();
app.UseAuthorization();
app.MapReverseProxy();

app.Run();
```

Puna tabela ruta: [gateway-tok.md](gateway-tok.md), sekcija 1.

---

## 7. Izuzetak: `Notifications` i `PdfGeneration` ostaju jedan projekat

Ova dva servisa su čisti RabbitMQ konzumenti bez API-ja i (uglavnom) bez baze — nemaju ni Api ni Data sloj u klasičnom smislu. Dijeljenje na 3 projekta bi ovdje bila čista ceremonija bez koristi (isti razlog zbog kojeg smo ranije odlučili da se ne pretjeruje sa fragmentacijom — vidi [backend-projekt-template.md](backend-projekt-template.md) sekcija 1).

```bash
cd backend/services/notifications
dotnet new worker -n eTicketing.Notifications
dotnet add package RabbitMQ.Client
dotnet add package MailKit
```

```
eTicketing.Notifications/
├── Worker.cs                 (BackgroundService, konzumira RabbitMQ)
├── EmailSender.cs
└── Templates/
```

Isto za `eTicketing.PdfGeneration` (`QuestPDF`, `Azure.Storage.Blobs` umjesto `MailKit`).

---

## 8. Docker — Dockerfile po servisu

Multi-stage build koji buildu treba **sva tri** projekta servisa (`.Api` je entry point, ali `.Business`/`.Data` se moraju kopirati da `dotnet restore`/`build` uspiju):

```dockerfile
# services/ticketing/eTicketing.Ticketing.Api/Dockerfile
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY shared/eTicketing.Contracts/*.csproj shared/eTicketing.Contracts/
COPY services/ticketing/eTicketing.Ticketing.Data/*.csproj services/ticketing/eTicketing.Ticketing.Data/
COPY services/ticketing/eTicketing.Ticketing.Business/*.csproj services/ticketing/eTicketing.Ticketing.Business/
COPY services/ticketing/eTicketing.Ticketing.Api/*.csproj services/ticketing/eTicketing.Ticketing.Api/
RUN dotnet restore services/ticketing/eTicketing.Ticketing.Api/eTicketing.Ticketing.Api.csproj

COPY shared/eTicketing.Contracts/ shared/eTicketing.Contracts/
COPY services/ticketing/ services/ticketing/
RUN dotnet publish services/ticketing/eTicketing.Ticketing.Api/eTicketing.Ticketing.Api.csproj -c Release -o /app

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /app .
ENTRYPOINT ["dotnet", "eTicketing.Ticketing.Api.dll"]
```

**`docker-compose.yml` (isječak za jedan servis + zajedničku infrastrukturu):**

```yaml
services:
  sqlserver:
    image: mcr.microsoft.com/mssql/server:2022-latest
    environment:
      SA_PASSWORD: "Your_password123"
      ACCEPT_EULA: "Y"
    networks: [eticketing-network]

  rabbitmq:
    image: rabbitmq:3-management
    ports: ["15672:15672"]   # samo management UI, ne javna API ruta
    networks: [eticketing-network]

  redis:
    image: redis:7-alpine
    networks: [eticketing-network]

  ticketing-service:
    build: ./backend/services/ticketing/eTicketing.Ticketing.Api
    environment:
      ConnectionStrings__TicketingDb: "Server=sqlserver;Database=TicketingDb;..."
      ConnectionStrings__Redis: "redis:6379"
      RabbitMq__Host: "rabbitmq"
      Services__Catalog: "http://catalog-service:8080"
      Services__Payment: "http://payment-service:8080"
    depends_on: [sqlserver, redis, rabbitmq]
    networks: [eticketing-network]
    # BEZ "ports:" — nije javno izložen, samo gateway ga poziva interno

  gateway:
    build: ./backend/gateway/eTicketing.Gateway
    ports: ["5000:8080"]     # JEDINI servis sa publikovanim portom
    depends_on: [identity-service, catalog-service, ticketing-service]
    networks: [eticketing-network]

networks:
  eticketing-network:
```

---

## 9. Checklist za svaki novi servis (kopiraj-adaptiraj)

- [ ] `dotnet new classlib -n X.Data`, `X.Business`; `dotnet new web -n X.Api` (ili `dotnet new worker` za Notifications/PdfGeneration)
- [ ] `dotnet sln add` za sva tri (ili jedan) projekta
- [ ] `dotnet add reference`: `Api → Business, Data, Contracts`; `Business → Data, Contracts`; `Data → Contracts`
- [ ] NuGet paketi po sekciji 3.3
- [ ] Entiteti + `DbContext` + `Configurations` u `.Data`
- [ ] Repository (nasljeđuje `Repository<T>` iz `Contracts`) u `.Data`
- [ ] Servisi + DTO-i + `Result<T>` u `.Business`
- [ ] `Program.cs` (DI wiring), `Endpoints/`, `Middleware/GlobalExceptionHandler` u `.Api`
- [ ] EF migracija (sekcija 4)
- [ ] Dockerfile + unos u `docker-compose.yml` (sekcija 8) — **bez** `ports:` osim za Gateway
- [ ] Ruta dodana u `eTicketing.Gateway` `appsettings.json` (ako servis treba javnu rutu — vidi [gateway-tok.md](gateway-tok.md))
