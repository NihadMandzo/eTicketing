# Template za backend mikroservise (Minimal API + Repository + Result + PagedResult)

> Odnosi se na **sve** backend servise — cijeli backend se piše ispočetka (vidi [arhitektura-migracija-mikroservisi-eda.md](arhitektura-migracija-mikroservisi-eda.md)): `eTicketing.Identity`, `eTicketing.Catalog`, `eTicketing.Ticketing`, `eTicketing.Payment`, `eTicketing.Notifications`, `eTicketing.PdfGeneration`, `eTicketing.Gateway`. Postojeći stari monolit (`backend/eTicketing.Api` i dr.) ostaje privremeno u repozitoriju samo kao referenca (kopiranje validacionih pravila/DTO oblika), i briše se na kraju (Sprint 5).

---

## 0. Bitno: kompatibilnost sa postojećim ugovorom za paginaciju

Postojeći kod već ima ustaljen oblik za paginaciju koji **oba** klijenta (Angular `PaginationBar`, Flutter Desktop `PaginationBar`) već očekuju:

```csharp
// backend/eTicketing.Model/SearchObjects/BaseSearchObject.cs (postojeće)
public class BaseSearchObject
{
    public int? Page { get; set; } = 0;      // 0-indeksirano!
    public int? PageSize { get; set; } = 10;
    public string? FTS { get; set; }
}

// backend/eTicketing.Model/Responses/PagedResponse.cs (postojeće)
public class PagedResponse<T>
{
    public List<T> Items { get; set; } = new();
    public int TotalCount { get; set; }
    public int? Page { get; set; }
    public int? PageSize { get; set; }
}
```

i `Skip(search.Page.Value * search.PageSize.Value)` (**ne** `(Page-1)*PageSize`, jer je `Page` 0-indeksirano).

**Novi `PagedResult<T>` u nastavku ovog dokumenta mora zadržati isti oblik i isto 0-indeksiranje** — u suprotnom se `PaginationBar` widget u Flutter Desktop app-u i paginacija u Angular komponentama moraju posebno granati po servisu, što je nepotreban trošak. Naziv klase može ostati `PagedResult<T>` u novim servisima (razlika u imenu nije bitna dok se JSON oblik poklapa), ali polja i indeksiranje moraju biti identični.

---

## 1. Odluka: 3-slojna arhitektura, tri odvojena projekta po servisu

**Finalna odluka: svaki servis (osim `Notifications`/`PdfGeneration`/`Gateway`) se sastoji od tri odvojena projekta — `.Api`, `.Business`, `.Data`** — klasična N-tier arhitektura, zavisnost u jednom smjeru: `Api → Business → Data`.

> Ranija verzija ovog dokumenta je preporučivala pojednostavljenu strukturu unutar jednog projekta (foldere umjesto zasebnih `.csproj`-a), zbog manje ceremonije za obim od 31 dana. Nakon eksplicitne odluke da se ide na fizički odvojene slojeve (API/Business/Data), ta preporuka je zamijenjena ovom. Fizička separacija u tri projekta i dalje nije "puna" Clean Architecture (nema Domain sloja bez zavisnosti, nema inverzije zavisnosti prema Data sloju) — to je namjerno pojednostavljenje: `Business` **direktno** referencira `Data` (ne obrnuto preko interfejsa), što je standardan i lako razumljiv N-tier obrazac, brži za graditi nego Clean/Onion Architecture, i i dalje jasno razdvaja odgovornosti.

**Tačna struktura, komande za kreiranje projekata, `dotnet add reference` wiring, NuGet paketi po sloju i Docker povezivanje: [backend-setup-guide.md](backend-setup-guide.md).** Ovaj dokument (template) ostaje izvor istine za **sadržaj** patterna (Result, Repository, PagedResult, Preview) — setup guide pokazuje **gdje fizički taj kod ide** kad je servis podijeljen na tri projekta.

Kratak pregled (detalji u setup guide-u):

| Projekat | Sadržaj iz ovog dokumenta koji ide ovdje |
|---|---|
| `.Data` | Entiteti (sekcija 2 dolje, bez `Domain/` prefiksa u imenu foldera — sad je to cijeli projekat), `DbContext`, `Repository<T>` (iz `Contracts`), Redis pristup, migracije |
| `.Business` | Servisi, DTO-i, `Result<T>` korištenje (sekcija 5), Preview obrazac (sekcija 8) |
| `.Api` | `Program.cs`, `Endpoints/` (sekcija 3), `Middleware/GlobalExceptionHandler` (sekcija 5) |
| `eTicketing.Contracts` (zaseban, dijeljen projekat) | `Result`/`Error`/`PagedResult`/`Repository<T>` generika (sekcije 5-7) |

---

## 2. Entiteti i domenski modeli — gdje žive

Entiteti (npr. `EventSector`, `Ticket`) žive u `.Data` projektu (`Entities/` folder), **ne** u posebnom `Domain` projektu — to je razlika u odnosu na Clean Architecture, gdje bi entiteti bili nezavisni od perzistencije. U ovoj (N-tier) arhitekturi entiteti su EF Core entiteti direktno (mogu imati EF-specifične navigacione property-je), a `.Business` sloj ih koristi kroz `Repository`/`IQueryable`, mapira u DTO-e (`Response` klase) prije nego što napuste `.Business` sloj — **entiteti nikad ne izlaze kroz `.Api` direktno**, uvijek se mapiraju u `Response` DTO u `.Business`.

Puna specifikacija foldera unutar svakog projekta: [backend-setup-guide.md](backend-setup-guide.md), sekcije 3.4–3.6.

---

## 3. Minimal API — organizacija

Umjesto Controller klasa, endpoint-i se grupišu u statičke extension metode po feature-u (`SectorEndpoints`, `TicketEndpoints`), registruju se u `Program.cs`. Ovo drži `Program.cs` kratkim i čitljivim.

**`Program.cs`:**

```csharp
var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<TicketingDbContext>(opt =>
    opt.UseSqlServer(builder.Configuration.GetConnectionString("TicketingDb")));

builder.Services.AddScoped<ISectorRepository, SectorRepository>();
builder.Services.AddScoped<ISectorService, SectorService>();
builder.Services.AddSingleton<IConnectionMultiplexer>(
    ConnectionMultiplexer.Connect(builder.Configuration.GetConnectionString("Redis")!));
builder.Services.AddScoped<ISectorCapacityLock, RedisSectorCapacityLock>();

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(/* isti signing key kao Identity & Catalog / Gateway */);
builder.Services.AddAuthorization();

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

**`Endpoints/SectorEndpoints.cs`:**

```csharp
public static class SectorEndpoints
{
    public static void MapSectorEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/sectors").WithTags("Sectors");

        group.MapGet("", GetSectors);                                   // javno
        group.MapPost("", CreateSector).RequireAuthorization("OrganizerOrAdmin");
        group.MapPost("/{id:int}/hold", HoldSector).RequireAuthorization();
    }

    private static async Task<IResult> GetSectors(
        [AsParameters] SectorQuery query, ISectorService service, CancellationToken ct)
    {
        var result = await service.GetPagedAsync(query, ct);
        return result.ToHttpResult();
    }

    private static async Task<IResult> CreateSector(
        CreateSectorRequest request, ISectorService service,
        ClaimsPrincipal user, CancellationToken ct)
    {
        var result = await service.CreateAsync(request, user, ct);
        return result.ToHttpResult(StatusCodes.Status201Created);
    }

    private static async Task<IResult> HoldSector(
        int id, HoldSectorRequest request, ISectorService service, CancellationToken ct)
    {
        var result = await service.HoldAsync(id, request.Quantity, ct);
        return result.ToHttpResult();
    }
}
```

`[AsParameters] SectorQuery` (npr. `EventId`, `Page`, `PageSize`, `FTS`) omogućava da se query string automatski bind-uje na isti oblik kao postojeći `BaseSearchObject`, bez ručnog parsiranja.

---

## 4. Repository Pattern

Generički repository + Unit of Work preko `DbContext` (EF Core `DbContext` je već Unit of Work — `SaveChangesAsync` se poziva **jednom**, na kraju handlera u servisu, ne unutar repozitorija, da više repo poziva u istom servisnom pozivu ostane u jednoj transakciji).

Ovaj generički dio (`IRepository<T>`, `Repository<T>`, `IUnitOfWork`) piše se **jednom**, u `eTicketing.Contracts/Persistence/` (ne po servisu) — svaki servis ga samo nasljeđuje. Zato `Repository<T>` mora raditi nad bilo kojim `DbContext`-om, ne nad konkretnim (`TicketingDbContext` itd.):

**`eTicketing.Contracts/Persistence/IRepository.cs` i `Repository.cs`:**

```csharp
public interface IRepository<T> where T : class
{
    Task<T?> GetByIdAsync(int id, CancellationToken ct = default);
    IQueryable<T> Query();
    Task AddAsync(T entity, CancellationToken ct = default);
    void Update(T entity);
    void Remove(T entity);
}

public class Repository<T> : IRepository<T> where T : class
{
    protected readonly DbContext Context;      // generički DbContext, ne konkretan tip
    protected readonly DbSet<T> DbSet;

    public Repository(DbContext context)
    {
        Context = context;
        DbSet = context.Set<T>();
    }

    public Task<T?> GetByIdAsync(int id, CancellationToken ct = default)
        => DbSet.FindAsync([id], ct).AsTask();

    public IQueryable<T> Query() => DbSet.AsQueryable();

    public Task AddAsync(T entity, CancellationToken ct = default)
        => DbSet.AddAsync(entity, ct).AsTask();

    public void Update(T entity) => DbSet.Update(entity);
    public void Remove(T entity) => DbSet.Remove(entity);
}

public interface IUnitOfWork
{
    Task<int> SaveChangesAsync(CancellationToken ct = default);
}
```

**Specifičan repository (živi u `.Data` projektu svakog servisa, npr. `eTicketing.Ticketing.Data/Repositories/`) kad treba nešto van generičkog CRUD-a** (npr. upit sa `.Include()` ili posebnom logikom):

```csharp
public interface ISectorRepository : IRepository<EventSector>
{
    Task<List<EventSector>> GetByEventIdAsync(int eventId, CancellationToken ct = default);
}

public class SectorRepository : Repository<EventSector>, ISectorRepository
{
    public SectorRepository(TicketingDbContext context) : base(context) { }   // konkretan DbContext ovdje je OK — .Data projekat ga poznaje

    public Task<List<EventSector>> GetByEventIdAsync(int eventId, CancellationToken ct = default)
        => Query().Where(s => s.EventId == eventId).ToListAsync(ct);
}
```

`TicketingDbContext` implementira `IUnitOfWork` direktno (`public class TicketingDbContext : DbContext, IUnitOfWork` — `DbContext` već ima `SaveChangesAsync`, samo se doda interfejs), pa se registruje u DI kao `IUnitOfWork` u `.Api` projektu (vidi [backend-setup-guide.md](backend-setup-guide.md), sekcija 3.6).

---

## 5. Result Pattern + globalni handler

Dvije odvojene stvari koje se ne miješaju:

- **Result Pattern** — za *očekivane* poslovne ishode (nije pronađeno, nema dovoljno kapaciteta, nevažeći podaci). Vraća se kao vrijednost, ne baca se exception — jasno se vidi iz potpisa metode (`Task<Result<T>>`) da poziv može "ne uspjeti" na predvidiv način.
- **Globalni exception handler middleware** — za *neočekivane* greške (baza nedostupna, bug, null reference). Ovdje se exception i dalje baca (to je ispravno za nešto što zaista nije trebalo da se desi), ali se hvata na jednom mjestu i pretvara u konzistentan JSON odgovor umjesto da curi stack trace klijentu.

**`eTicketing.Contracts/Results/Error.cs` i `Result.cs`:**

```csharp
public enum ErrorType { None, Validation, NotFound, Conflict, Unauthorized, Failure }

public sealed class Error
{
    public string Code { get; }
    public string Message { get; }
    public ErrorType Type { get; }

    private Error(string code, string message, ErrorType type) =>
        (Code, Message, Type) = (code, message, type);

    public static Error NotFound(string code, string message) => new(code, message, ErrorType.NotFound);
    public static Error Validation(string code, string message) => new(code, message, ErrorType.Validation);
    public static Error Conflict(string code, string message) => new(code, message, ErrorType.Conflict);
    public static Error Unauthorized(string code, string message) => new(code, message, ErrorType.Unauthorized);
    public static readonly Error None = new(string.Empty, string.Empty, ErrorType.None);
}

public class Result
{
    public bool IsSuccess { get; }
    public bool IsFailure => !IsSuccess;
    public Error Error { get; }

    protected Result(bool isSuccess, Error error)
    {
        if (isSuccess && error != Error.None) throw new InvalidOperationException();
        if (!isSuccess && error == Error.None) throw new InvalidOperationException();
        IsSuccess = isSuccess;
        Error = error;
    }

    public static Result Success() => new(true, Error.None);
    public static Result Failure(Error error) => new(false, error);
}

public sealed class Result<T> : Result
{
    public T? Value { get; }
    private Result(T value) : base(true, Error.None) => Value = value;
    private Result(Error error) : base(false, error) => Value = default;

    public static Result<T> Success(T value) => new(value);
    public static Result<T> Failure(Error error) => new(error);
    public static implicit operator Result<T>(T value) => Success(value);
}
```

**`eTicketing.Contracts/Results/ResultExtensions.cs` — mapiranje Result → HTTP odgovor (Minimal API `IResult`):**

```csharp
using Microsoft.AspNetCore.Http;
using Http = Microsoft.AspNetCore.Http.Results;
// Napomena: namespace ovog fajla ("...Results") se poklapa sa imenom klase
// Microsoft.AspNetCore.Http.Results, pa se ona referencira preko alias-a "Http"
// da bi se izbjegao sudar imena (compiler bi inače "Results" unutar namespace-a
// "...Results" tumačio kao trenutni namespace, ne kao ASP.NET Core klasu).

public static class ResultExtensions
{
    public static IResult ToHttpResult(this Result result, int successStatusCode = StatusCodes.Status200OK)
    {
        if (result.IsSuccess) return Http.StatusCode(successStatusCode);
        return ToProblemResult(result.Error);
    }

    public static IResult ToHttpResult<T>(this Result<T> result, int successStatusCode = StatusCodes.Status200OK)
    {
        if (result.IsSuccess) return Http.Json(result.Value, statusCode: successStatusCode);
        return ToProblemResult(result.Error);
    }

    private static IResult ToProblemResult(Error error) => error.Type switch
    {
        ErrorType.NotFound => Http.NotFound(new { error.Code, error.Message }),
        ErrorType.Validation => Http.BadRequest(new { error.Code, error.Message }),
        ErrorType.Conflict => Http.Conflict(new { error.Code, error.Message }),
        ErrorType.Unauthorized => Http.Json(new { error.Code, error.Message }, statusCode: StatusCodes.Status403Forbidden),
        _ => Http.Problem(title: error.Message, statusCode: StatusCodes.Status500InternalServerError)
    };
}
```

Primjer korištenja u servisu (US-1.3 — hold sa Redis lockom, konkretan primjer kako Result nosi poslovnu grešku "nema kapaciteta" bez exception-a):

```csharp
public async Task<Result<HoldResponse>> HoldAsync(int sectorId, int quantity, CancellationToken ct)
{
    var sector = await _repository.GetByIdAsync(sectorId, ct);
    if (sector is null)
        return Result<HoldResponse>.Failure(Error.NotFound("sector.not_found", "Sektor nije pronađen."));

    var acquired = await _capacityLock.TryHoldAsync(sectorId, quantity, ttl: TimeSpan.FromMinutes(5), ct);
    if (!acquired)
        return Result<HoldResponse>.Failure(Error.Conflict("sector.no_capacity", "Nema dovoljno slobodnih mjesta."));

    return Result<HoldResponse>.Success(new HoldResponse(sectorId, quantity, DateTime.UtcNow.AddMinutes(5)));
}
```

**`Middleware/GlobalExceptionHandler.cs` (za neočekivane greške, .NET 8+ `IExceptionHandler`):**

```csharp
public class GlobalExceptionHandler : IExceptionHandler
{
    private readonly ILogger<GlobalExceptionHandler> _logger;

    public GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger) => _logger = logger;

    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext, Exception exception, CancellationToken ct)
    {
        _logger.LogError(exception, "Neobrađen izuzetak na {Path}", httpContext.Request.Path);

        httpContext.Response.StatusCode = StatusCodes.Status500InternalServerError;
        await httpContext.Response.WriteAsJsonAsync(new
        {
            code = "internal_error",
            message = "Došlo je do neočekivane greške. Pokušajte kasnije."
        }, cancellationToken: ct);

        return true;
    }
}
```

Ovo je konceptualni ekvivalent postojećeg `GlobalExceptionHandlerMiddleware` iz `eTicketing.Api` (samo u `IExceptionHandler` obliku koji je prirodniji za Minimal API projekte bez klasičnog middleware pipeline-a vezanog za MVC).

---

## 6. PagedResult Pattern

Kao što je objašnjeno u sekciji 0, oblik i indeksiranje **moraju odgovarati postojećem `PagedResponse<T>`/`BaseSearchObject` ugovoru** iz `eTicketing.Model`.

**`eTicketing.Contracts/Pagination/PagedResult.cs`:**

```csharp
public class PagedResult<T>
{
    public List<T> Items { get; init; } = [];
    public int TotalCount { get; init; }
    public int Page { get; init; }
    public int PageSize { get; init; }
}
```

**`eTicketing.Contracts/Pagination/QueryableExtensions.cs` — kreiranje `PagedResult<T>` iz `IQueryable<T>`:**

```csharp
public static class QueryableExtensions
{
    public static async Task<PagedResult<T>> ToPagedResultAsync<T>(
        this IQueryable<T> query, int page, int pageSize, CancellationToken ct = default)
    {
        var totalCount = await query.CountAsync(ct);

        var items = await query
            .Skip(page * pageSize)     // 0-indeksirano, isto kao postojeći BaseService
            .Take(pageSize)
            .ToListAsync(ct);

        return new PagedResult<T>
        {
            Items = items,
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize
        };
    }
}
```

**Korištenje u servisu (`GET /sectors`):**

```csharp
public async Task<Result<PagedResult<SectorResponse>>> GetPagedAsync(SectorQuery query, CancellationToken ct)
{
    var page = query.Page ?? 0;
    var pageSize = query.PageSize ?? 10;

    var q = _repository.Query()
        .Where(s => query.EventId == null || s.EventId == query.EventId)
        .OrderBy(s => s.Name)
        .Select(s => new SectorResponse(s.Id, s.Name, s.Capacity, s.AvailableCount, s.Price));

    var paged = await q.ToPagedResultAsync(page, pageSize, ct);
    return Result<PagedResult<SectorResponse>>.Success(paged);
}
```

Time je `GET /sectors?eventId=3&page=0&pageSize=10` odgovor **oblikom identičan** postojećim paginiranim odgovorima iz monolita — Angular i Flutter Desktop komponente za paginaciju rade bez izmjene i protiv novih servisa.

---

## 7. Sažetak — šta ide u `eTicketing.Contracts` (dijeljeno između svih servisa)

Da se `Result`, `PagedResult`, `Error`, `IRepository<T>`/`Repository<T>` ne pišu iznova u svakom servisu, stoje u jednom zajedničkom projektu — `eTicketing.Contracts` (iz [SPRINT_1.md](../SPRINTS/SPRINT_1.md), US-1.1) — pored event modela (`TicketPurchased`, itd.):

```
eTicketing.Contracts/
├── Events/                  (RabbitMQ event modeli — TicketPurchased, ...)
├── Results/                 (Error, Result, Result<T>, ResultExtensions)
├── Pagination/              (PagedResult<T>, QueryableExtensions)
└── Persistence/             (IRepository<T>, Repository<T>, IUnitOfWork)
```

Svaki servisni projekat (`.Data`, `.Business`, `.Api`) referencira `eTicketing.Contracts` kao `ProjectReference` i dobija sve gore navedeno besplatno, bez duplikacije koda. Tačne `dotnet` komande za kreiranje i povezivanje: [backend-setup-guide.md](backend-setup-guide.md).

---

## 8. Obrazac: Preview prije objavljivanja/izmjene (Event, EventSector)

Funkcionalni zahtjev: organizator mora imati "preview" prije nego što objavi ili sačuva izmjenu eventa/sektora. Ovo se implementira kao **stateless endpoint koji poziva istu validaciju kao create/update, ali ne upisuje ništa u bazu** — čist primjer gdje Result Pattern nosi vrijednost i bez perzistencije.

**Entitet dobija `Status` enum:**

```csharp
public enum PublishStatus { Draft, Published }
```

**Servis izdvaja validaciju u zajedničku privatnu metodu koju koriste i `Preview` i `Create`/`Update`:**

```csharp
public interface IEventService
{
    Task<Result<EventPreviewResponse>> PreviewAsync(UpsertEventRequest request, ClaimsPrincipal user, CancellationToken ct);
    Task<Result<EventResponse>> CreateAsync(UpsertEventRequest request, ClaimsPrincipal user, CancellationToken ct);
    Task<Result<EventResponse>> UpdateAsync(int id, UpsertEventRequest request, ClaimsPrincipal user, CancellationToken ct);
    Task<Result> PublishAsync(int id, ClaimsPrincipal user, CancellationToken ct);
}

public class EventService : IEventService
{
    public async Task<Result<EventPreviewResponse>> PreviewAsync(
        UpsertEventRequest request, ClaimsPrincipal user, CancellationToken ct)
    {
        var validation = Validate(request);              // ista validacija kao Create/Update
        if (validation.IsFailure)
            return Result<EventPreviewResponse>.Failure(validation.Error);

        // NE upisuje u bazu — samo vraća kako bi event izgledao
        return Result<EventPreviewResponse>.Success(EventPreviewResponse.From(request));
    }

    public async Task<Result<EventResponse>> CreateAsync(
        UpsertEventRequest request, ClaimsPrincipal user, CancellationToken ct)
    {
        var validation = Validate(request);
        if (validation.IsFailure)
            return Result<EventResponse>.Failure(validation.Error);

        var entity = request.ToEntity(organizationId: user.GetOrganizationId(), status: PublishStatus.Draft);
        await _repository.AddAsync(entity, ct);
        await _unitOfWork.SaveChangesAsync(ct);

        return Result<EventResponse>.Success(EventResponse.From(entity));
    }

    public async Task<Result> PublishAsync(int id, ClaimsPrincipal user, CancellationToken ct)
    {
        var entity = await _repository.GetByIdAsync(id, ct);
        if (entity is null)
            return Result.Failure(Error.NotFound("event.not_found", "Event nije pronađen."));

        if (!user.IsPlatformStaff() && entity.OrganizationId != user.GetOrganizationId())
            return Result.Failure(Error.Unauthorized("event.forbidden", "Event ne pripada vašoj organizaciji."));

        entity.Status = PublishStatus.Published;
        await _unitOfWork.SaveChangesAsync(ct);
        return Result.Success();
    }

    private static Result Validate(UpsertEventRequest request) { /* zajednička pravila */ }
}
```

**Endpoints:**

```csharp
group.MapPost("/preview", Preview).RequireAuthorization("Organizer");
group.MapPost("", Create).RequireAuthorization("Organizer");
group.MapPost("/{id:int}/publish", Publish).RequireAuthorization("Organizer");
group.MapPut("/{id:int}", Update).RequireAuthorization("Organizer");
group.MapDelete("/{id:int}", Delete).RequireAuthorization("Organizer");

group.MapGet("", GetPublished).AllowAnonymous();          // samo Status=Published
group.MapGet("/mine", GetMine).RequireAuthorization("Organizer");   // sve svoje, svi statusi
group.MapGet("/all", GetAll).RequireAuthorization("PlatformStaff"); // sve, svih organizacija
```

Tok na klijentu (Desktop app): popuni formu → **Preview** (`POST /events/preview`, prikaže rezultat bez snimanja) → **Sačuvaj kao draft** (`POST /events`) → kasnije **Objavi** (`POST /events/{id}/publish`). Za izmjenu postojećeg: popuni izmjene → **Preview** (isti `POST /events/preview` sa izmijenjenim podacima) → **Sačuvaj izmjene** (`PUT /events/{id}`).

Identičan obrazac se ponavlja za `EventSector` u `eTicketing.Ticketing` (`POST /sectors/preview`, `POST /sectors`, `POST /sectors/{id}/publish`, `PUT /sectors/{id}`).
