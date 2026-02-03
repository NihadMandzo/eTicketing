using System.Text;
using eTicketing.Api.Filters;
using eTicketing.Api.Middleware;
using eTicketing.Services.Database;
using eTicketing.Services.Helpers;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

// Database Configuration
builder.Services.AddDbContext<eTicketingDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

// JWT Authentication
var jwtSettings = builder.Configuration.GetSection("JwtSettings");
var secretKey = jwtSettings["SecretKey"] ?? throw new InvalidOperationException("JWT SecretKey is not configured");

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
        ValidIssuer = jwtSettings["Issuer"] ?? throw new InvalidOperationException("JWT Issuer is not configured"),
        ValidAudience = jwtSettings["Audience"] ?? throw new InvalidOperationException("JWT Audience is not configured"),
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

// HttpContextAccessor (required for JwtHelper and HttpHelper)
builder.Services.AddHttpContextAccessor();

// AutoMapper
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

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference();
}

app.UseHttpsRedirection();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();
