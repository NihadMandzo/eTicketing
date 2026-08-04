using System.Text;
using eTicketing.Identity.Api.Endpoints;
using eTicketing.Identity.Api.Infrastructure;
using eTicketing.Identity.Api.Middleware;
using eTicketing.Identity.Business.Admins;
using eTicketing.Identity.Business.Auth;
using eTicketing.Identity.Business.Organizations;
using eTicketing.Identity.Business.Security;
using eTicketing.Identity.Data;
using eTicketing.Identity.Data.Repositories;
using eTicketing.Contracts.Persistence;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

// --- Data sloj ---
builder.Services.AddDbContext<IdentityDbContext>(opt =>
    opt.UseSqlServer(builder.Configuration.GetConnectionString("IdentityDb")));
builder.Services.AddScoped<IUnitOfWork>(sp => sp.GetRequiredService<IdentityDbContext>());
builder.Services.AddScoped<IUserRepository, UserRepository>();
builder.Services.AddScoped<IOrganizationRepository, OrganizationRepository>();

// --- Business sloj ---
builder.Services.Configure<JwtOptions>(builder.Configuration.GetSection(JwtOptions.SectionName));
builder.Services.AddScoped<IJwtTokenGenerator, JwtTokenGenerator>();
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IOrganizationService, OrganizationService>();
builder.Services.AddScoped<IAdminService, AdminService>();
builder.Services.AddSingleton<IEventPublisher, RabbitMqEventPublisher>();

// --- Auth ---
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
builder.Services.AddHealthChecks().AddDbContextCheck<IdentityDbContext>("database");
builder.Services.AddOpenApi();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference();
}

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
    await db.Database.MigrateAsync();
}

app.UseExceptionHandler();
app.UseAuthentication();
app.UseAuthorization();

app.MapAuthEndpoints();
app.MapOrganizationEndpoints();
app.MapAdminEndpoints();
app.MapHealthChecks("/health");

app.Run();
