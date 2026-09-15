using eTicketing.Contracts.Validation;
using eTicketing.Contracts.Hosting;
using eTicketing.Payment.Api.Endpoints;
using eTicketing.Payment.Api.Infrastructure;
using eTicketing.Payment.Data;
using Microsoft.EntityFrameworkCore;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

builder.AddPaymentInfrastructure();
builder.AddPlatformApiEssentials<PaymentDbContext>();

// Napomena: Payment servis NEMA javnu rutu kroz Gateway (vidi docs/gateway-tok.md) —
// poziva ga isključivo eTicketing.Ticketing, interno preko Docker network-a.
// Zato ovdje namjerno NEMA JWT autentikacije — izolacija se postiže mrežnom segmentacijom
// (servis nema publikovan port ka hostu u docker-compose.yml), ne JWT provjerom.

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

app.MapPaymentEndpoints();
app.MapHealthChecks("/health");

// Fails fast if any route declared WithValidation<T>() without a registered IValidator<T>;
// that combination is otherwise silent, leaving the endpoint unvalidated while looking validated.
app.VerifyRequestValidatorsRegistered();

app.Run();
