using eTicketing.Contracts.Validation;
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
// Before UseAuthentication, and before the rate limiter that is the whole reason it is here:
// everything downstream reads Connection.RemoteIpAddress, and until this runs that is the
// Gateway's container address for every single request. See ForwardedHeadersExtensions.
app.UseForwardedHeaders();
app.UseAuthentication();
app.UseAuthorization();
app.UseRateLimiter();

app.MapAuthEndpoints();
app.MapOrganizationEndpoints();
app.MapAdminEndpoints();
app.MapHealthChecks("/health");

// Fails fast if any route declared WithValidation<T>() without a registered IValidator<T>;
// that combination is otherwise silent, leaving the endpoint unvalidated while looking validated.
app.VerifyRequestValidatorsRegistered();

app.Run();
