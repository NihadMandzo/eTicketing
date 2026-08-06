using eTicketing.Contracts.Hosting;
using eTicketing.Identity.Api.Endpoints;
using eTicketing.Identity.Api.Infrastructure;
using eTicketing.Identity.Data;
using eTicketing.Shared.Auth;
using Microsoft.EntityFrameworkCore;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

builder.AddIdentityInfrastructure();
builder.Services.AddSharedJwtBearerAuthentication(builder.Configuration);
builder.Services.AddPlatformAuthorizationPolicies();
builder.AddPlatformApiEssentials<IdentityDbContext>();

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
