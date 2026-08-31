using eTicketing.Contracts.Hosting;
using eTicketing.Shared.Auth;
using eTicketing.Ticketing.Api.Endpoints;
using eTicketing.Ticketing.Api.Infrastructure;
using eTicketing.Ticketing.Data;
using Microsoft.EntityFrameworkCore;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

builder.AddTicketingInfrastructure();
builder.Services.AddSharedJwtBearerAuthentication(builder.Configuration);
builder.Services.AddPlatformAuthorizationPolicies();
builder.AddPlatformApiEssentials<TicketingDbContext>();

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

app.MapSectorEndpoints();
app.MapTicketTypeEndpoints();
app.MapPurchaseEndpoints();
app.MapTicketEndpoints();
app.MapTicketPrintEndpoints();
app.MapReportEndpoints();
app.MapHealthChecks("/health");

app.Run();
