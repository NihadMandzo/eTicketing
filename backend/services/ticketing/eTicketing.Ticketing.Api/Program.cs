using System.Text;
using eTicketing.Ticketing.Api.Middleware;
using eTicketing.Ticketing.Data;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

// --- Data sloj ---
builder.Services.AddDbContext<TicketingDbContext>(opt =>
    opt.UseSqlServer(builder.Configuration.GetConnectionString("TicketingDb")));

// TODO (Sprint 2, US-2.4/2.5): registrovati ISectorRepository, ISectorCapacityLock (Redis) i
// ISectorService iz .Business projekta ovdje, kad entiteti budu dodani.
// TODO (Sprint 3, US-3.2/3.3): HttpClient + Polly circuit breaker ka eTicketing.Payment,
// HttpClient + Polly retry/timeout ka eTicketing.Catalog (provjera vlasništva eventa).

// --- Auth (isti signing key kao Identity/Gateway) ---
var jwtKey = builder.Configuration["Jwt:SigningKey"]
    ?? throw new InvalidOperationException("Jwt:SigningKey nije konfigurisan.");

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey)),
            ValidateIssuer = false,
            ValidateAudience = false,
            ValidateLifetime = true
        };
    });

builder.Services.AddAuthorizationBuilder()
    .AddPolicy("SuperAdminOnly", p => p.RequireRole("SuperAdmin"))
    .AddPolicy("PlatformStaff", p => p.RequireRole("Admin", "SuperAdmin"))
    .AddPolicy("Organizer", p => p.RequireRole("OrganizationSuperAdmin", "OrganizationAdmin", "Admin", "SuperAdmin"));

// --- Middleware, health, docs ---
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.AddProblemDetails();
builder.Services.AddHealthChecks().AddDbContextCheck<TicketingDbContext>("database");
builder.Services.AddOpenApi();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference();
}

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<TicketingDbContext>();
    await db.Database.MigrateAsync();
}

app.UseExceptionHandler();
app.UseAuthentication();
app.UseAuthorization();

// TODO (Sprint 2/3): app.MapSectorEndpoints(); app.MapTicketEndpoints(); app.MapPurchaseEndpoints();
app.MapHealthChecks("/health");

app.Run();
