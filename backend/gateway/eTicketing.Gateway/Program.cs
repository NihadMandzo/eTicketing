using eTicketing.Shared.Auth;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddReverseProxy()
    .LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"));

builder.Services.AddSharedJwtBearerAuthentication(builder.Configuration);

// Defence in depth, not the authorization model: every role/ownership decision still belongs to
// the downstream service, which re-checks it. This only refuses obviously-unauthenticated traffic
// at the edge for the route prefixes where *every* endpoint requires a session, so a future
// endpoint added to one of those groups without its own .RequireAuthorization can't be reachable
// anonymously. Routes deliberately left without a policy in appsettings.json:
//   - /api/auth, /api/products, /api/categories, /api/sectors, /api/recommendations,
//     /api/organizations — each mixes anonymous reads (public browsing, login, the storefront's
//     organizer card) with authenticated writes, so a blanket policy would break browsing.
//   - /api/gate — authenticates with the GateDevice scheme (X-Device-Key), which is registered in
//     Ticketing and does not exist here; an edge policy would reject every real scanner.
//   - /api/payments/webhook — the provider has no cookie; it signs the raw body instead.
builder.Services.AddAuthorization(options =>
    options.AddPolicy("authenticated", policy => policy.RequireAuthenticatedUser()));

var corsOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
builder.Services.AddCors(options => options.AddDefaultPolicy(policy =>
    policy.WithOrigins(corsOrigins).AllowAnyHeader().AllowAnyMethod().AllowCredentials()));

var app = builder.Build();

app.UseCors();
app.UseAuthentication();
app.UseAuthorization();

app.MapGet("/", () => "eTicketing.Gateway");
app.MapReverseProxy();

app.Run();
