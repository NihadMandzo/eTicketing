using System.Text;
using eTicketing.Api.Filters;
using eTicketing.Api.Middleware;
using eTicketing.Services.Database;
using eTicketing.Services.Helpers;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Scalar.AspNetCore;
using Asp.Versioning;

// Load .env file
DotNetEnv.Env.Load();

var builder = WebApplication.CreateBuilder(args);

// Override configuration with environment variables
builder.Configuration.AddEnvironmentVariables();

// Add services to the container.

// Build connection string from environment variables
var connectionString = $"Server={Environment.GetEnvironmentVariable("DB_SERVER") ?? "."};Database={Environment.GetEnvironmentVariable("DB_NAME") ?? "eTicketingDB"};Trusted_Connection={Environment.GetEnvironmentVariable("DB_TRUSTED_CONNECTION") ?? "True"};TrustServerCertificate={Environment.GetEnvironmentVariable("DB_TRUST_SERVER_CERTIFICATE") ?? "True"};MultipleActiveResultSets={Environment.GetEnvironmentVariable("DB_MULTIPLE_ACTIVE_RESULT_SETS") ?? "true"}";

// Database Configuration
builder.Services.AddDbContext<eTicketingDbContext>(options =>
    options.UseSqlServer(connectionString));

// Health Checks
builder.Services.AddHealthChecks()
    .AddDbContextCheck<eTicketingDbContext>("database");

// JWT Authentication
var secretKey = Environment.GetEnvironmentVariable("JWT_SECRET_KEY") 
    ?? throw new InvalidOperationException("JWT SecretKey is not configured in .env file");

// Validate secret key length (must be at least 32 bytes for HS256)
if (Encoding.UTF8.GetByteCount(secretKey) < 32)
{
    throw new InvalidOperationException("JWT SecretKey must be at least 32 bytes (characters) long for secure token signing");
}

var issuer = Environment.GetEnvironmentVariable("JWT_ISSUER") 
    ?? throw new InvalidOperationException("JWT Issuer is not configured in .env file");
var audience = Environment.GetEnvironmentVariable("JWT_AUDIENCE") 
    ?? throw new InvalidOperationException("JWT Audience is not configured in .env file");

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        ValidIssuer = issuer,
        ValidAudience = audience,
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey))
    };

    options.Events = new JwtBearerEvents
    {
        OnChallenge = context =>
        {
            context.HandleResponse();
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            return context.Response.WriteAsJsonAsync(new
            {
                statusCode = 401,
                message = "Neautorizovan pristup"
            });
        },
        OnForbidden = context =>
        {
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            return context.Response.WriteAsJsonAsync(new
            {
                statusCode = 403,
                message = "Zabranjen pristup"
            });
        }
    };
});

builder.Services.AddAuthorization();

// CORS Configuration
var corsOriginsEnv = Environment.GetEnvironmentVariable("CORS_ORIGINS");
string[] corsOrigins;

if (string.IsNullOrWhiteSpace(corsOriginsEnv))
{
    corsOrigins = ["http://localhost:4200", "http://localhost:3000"];
}
else
{
    var parsedOrigins = corsOriginsEnv.Split(',', StringSplitOptions.RemoveEmptyEntries)
                                      .Select(o => o.Trim())
                                      .Where(o => !string.IsNullOrWhiteSpace(o))
                                      .ToArray();
    
    // Validate each origin is a well-formed URI
    var invalidOrigins = new List<string>();
    var validOrigins = new List<string>();
    
    foreach (var origin in parsedOrigins)
    {
        if (Uri.TryCreate(origin, UriKind.Absolute, out var uri) && 
            (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps))
        {
            validOrigins.Add(origin);
        }
        else
        {
            invalidOrigins.Add(origin);
        }
    }
    
    if (invalidOrigins.Any())
    {
        throw new InvalidOperationException(
            $"Invalid CORS origins detected in CORS_ORIGINS environment variable: {string.Join(", ", invalidOrigins)}. " +
            "All origins must be valid absolute HTTP or HTTPS URLs.");
    }
    
    corsOrigins = validOrigins.ToArray();
    
    
     if (corsOrigins.Length == 0)
    {
        throw new InvalidOperationException(
            "CORS_ORIGINS environment variable is set but contains no valid origins. " +
            "Provide valid HTTP/HTTPS URLs or unset the variable to use defaults.");
    }
}
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowFrontend", policy =>
    {
        policy.WithOrigins(corsOrigins)
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials();
    });
});

// API Versioning
builder.Services.AddApiVersioning(options =>
{
    options.DefaultApiVersion = new ApiVersion(1, 0);
    options.AssumeDefaultVersionWhenUnspecified = true;
    options.ReportApiVersions = true;
    options.ApiVersionReader = new UrlSegmentApiVersionReader();
}).AddApiExplorer(options =>
{
    options.GroupNameFormat = "'v'VVV";
    options.SubstituteApiVersionInUrl = true;
});

// HttpContextAccessor (required for JwtHelper and HttpHelper)
builder.Services.AddHttpContextAccessor();

// AutoMapper (scans all loaded assemblies for profiles)
builder.Services.AddAutoMapper(AppDomain.CurrentDomain.GetAssemblies());

// Helpers
builder.Services.AddScoped<JwtHelper>();
builder.Services.AddScoped<AuthorizationHelper>();

// Services
builder.Services.AddScoped<eTicketing.Services.Interfaces.IAuthService, eTicketing.Services.Services.AuthService>();
builder.Services.AddScoped<eTicketing.Services.Interfaces.ICategoryService, eTicketing.Services.Services.CategoryService>();
builder.Services.AddScoped<eTicketing.Services.Interfaces.IOrganizationService, eTicketing.Services.Services.OrganizationService>();
builder.Services.AddScoped<eTicketing.Services.Interfaces.IBlobStorageService, eTicketing.Services.Services.BlobStorageService>();
builder.Services.AddScoped<eTicketing.Services.Interfaces.IEventService, eTicketing.Services.Services.EventService>();

builder.Services.AddControllers(options =>
{
    options.Filters.Add<ApiResponseFilter>();
});
builder.Services.AddOpenApi();

var app = builder.Build();

// Ensure database is created
using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<eTicketingDbContext>();
    try
    {
        // Apply migrations automatically
        await dbContext.Database.MigrateAsync();
        
        Console.WriteLine("Database migrations applied successfully.");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Database migration failed: {ex.Message}");
        throw;
    }
}

// Configure the HTTP request pipeline.

// Global Exception Handler (must be first)
app.UseMiddleware<GlobalExceptionHandlerMiddleware>();

// CORS (must be before authentication)
app.UseCors("AllowFrontend");

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference();
}

app.UseHttpsRedirection();

app.UseAuthentication();
app.UseAuthorization();

// Health Check endpoint
app.MapHealthChecks("/health");

app.MapControllers();

app.Run();
