using eTicketing.Catalog.Api.Endpoints;
using eTicketing.Catalog.Api.Infrastructure;
using eTicketing.Catalog.Data;
using eTicketing.Contracts.Hosting;
using eTicketing.Shared.Auth;
using Microsoft.EntityFrameworkCore;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

builder.AddCatalogInfrastructure();
builder.Services.AddSharedJwtBearerAuthentication(builder.Configuration);
builder.Services.AddPlatformAuthorizationPolicies();
builder.AddPlatformApiEssentials<CatalogDbContext>();

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

app.MapCategoryEndpoints();
app.MapEventEndpoints();
app.MapHealthChecks("/health");

app.Run();
