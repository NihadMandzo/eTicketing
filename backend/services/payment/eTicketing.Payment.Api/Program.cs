using eTicketing.Payment.Api.Middleware;
using eTicketing.Payment.Data;
using Microsoft.EntityFrameworkCore;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

// --- Data sloj ---
builder.Services.AddDbContext<PaymentDbContext>(opt =>
    opt.UseSqlServer(builder.Configuration.GetConnectionString("PaymentDb")));

// TODO (Sprint 3, US-3.2): registrovati IPaymentRepository i IPaymentService kad entiteti budu dodani.

// Napomena: Payment servis NEMA javnu rutu kroz Gateway (vidi docs/gateway-tok.md) —
// poziva ga isključivo eTicketing.Ticketing, interno preko Docker network-a.
// Zato ovdje namjerno NEMA JWT autentikacije — izolacija se postiže mrežnom segmentacijom
// (servis nema publikovan port ka hostu u docker-compose.yml), ne JWT provjerom.

builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.AddProblemDetails();
builder.Services.AddHealthChecks().AddDbContextCheck<PaymentDbContext>("database");
builder.Services.AddOpenApi();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference();
}

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<PaymentDbContext>();
    await db.Database.MigrateAsync();
}

app.UseExceptionHandler();

// TODO (Sprint 3): app.MapPaymentEndpoints();
app.MapHealthChecks("/health");

app.Run();
