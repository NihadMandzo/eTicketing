using System.Text;
using eTicketing.Catalog.Api.Middleware;
using eTicketing.Catalog.Data;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

// --- Data sloj ---
builder.Services.AddDbContext<CatalogDbContext>(opt =>
    opt.UseSqlServer(builder.Configuration.GetConnectionString("CatalogDb")));

// TODO (Sprint 2, US-2.1/2.2/2.3): registrovati ICategoryRepository/IEventRepository i
// ICategoryService/IEventService iz .Business projekta ovdje, kad entiteti budu dodani.

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
builder.Services.AddHealthChecks().AddDbContextCheck<CatalogDbContext>("database");
builder.Services.AddOpenApi();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference();
}

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<CatalogDbContext>();
    await db.Database.MigrateAsync();
}

app.UseExceptionHandler();
app.UseAuthentication();
app.UseAuthorization();

// TODO (Sprint 2): app.MapCategoryEndpoints(); app.MapEventEndpoints();
app.MapHealthChecks("/health");

app.Run();
