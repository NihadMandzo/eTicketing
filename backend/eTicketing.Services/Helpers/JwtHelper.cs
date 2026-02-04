using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using eTicketing.Services.Database.Entities;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;

namespace eTicketing.Services.Helpers;

public class JwtHelper
{
    private readonly IConfiguration _configuration;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public JwtHelper(IConfiguration configuration, IHttpContextAccessor httpContextAccessor)
    {
        _configuration = configuration;
        _httpContextAccessor = httpContextAccessor;
    }

    public string GenerateToken(User user)
    {
        var secretKey = Environment.GetEnvironmentVariable("JWT_SECRET_KEY")
            ?? throw new InvalidOperationException("JWT SecretKey is not configured in .env file");
        var issuer = Environment.GetEnvironmentVariable("JWT_ISSUER")
            ?? throw new InvalidOperationException("JWT Issuer is not configured in .env file");
        var audience = Environment.GetEnvironmentVariable("JWT_AUDIENCE")
            ?? throw new InvalidOperationException("JWT Audience is not configured in .env file");
        
        var expirationMinutesStr = Environment.GetEnvironmentVariable("JWT_EXPIRATION_MINUTES");
        var expirationMinutes = int.TryParse(expirationMinutesStr, out var parsedMinutes) ? parsedMinutes : 1440;
        
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new List<Claim>
        {
            new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new Claim(ClaimTypes.Email, user.Email),
            new Claim(ClaimTypes.Name, $"{user.FirstName} {user.LastName}"),
            new Claim("FirstName", user.FirstName),
            new Claim("LastName", user.LastName)
        };

        // Add role claim only if Role is loaded
        if (user.Role != null)
        {
            claims.Add(new Claim(ClaimTypes.Role, user.Role.Name));
        }

        if (user.OrganizationId.HasValue)
        {
            claims.Add(new Claim("OrganizationId", user.OrganizationId.Value.ToString()));
        }

        var expiration = DateTime.UtcNow.AddMinutes(expirationMinutes);

        var token = new JwtSecurityToken(
            issuer: issuer,
            audience: audience,
            claims: claims,
            expires: expiration,
            signingCredentials: credentials
        );

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    public DateTime GetTokenExpiration()
    {
        var expirationMinutes = int.Parse(Environment.GetEnvironmentVariable("JWT_EXPIRATION_MINUTES") ?? "1440");
        return DateTime.UtcNow.AddMinutes(expirationMinutes);
    }

    /// <summary>
    /// Gets the current user ID from JWT token claims
    /// </summary>
    public int? GetUserId()
    {
        var userIdClaim = _httpContextAccessor.HttpContext?.User?.FindFirst(ClaimTypes.NameIdentifier);
        
        if (userIdClaim != null && int.TryParse(userIdClaim.Value, out int userId))
        {
            return userId;
        }

        return null;
    }

    /// <summary>
    /// Gets the current organization ID from JWT token claims
    /// </summary>
    public int? GetOrganizationId()
    {
        var organizationIdClaim = _httpContextAccessor.HttpContext?.User?.FindFirst("OrganizationId");
        
        if (organizationIdClaim != null && int.TryParse(organizationIdClaim.Value, out int organizationId))
        {
            return organizationId;
        }

        return null;
    }

    /// <summary>
    /// Gets the current user's email from JWT token claims
    /// </summary>
    public string? GetUserEmail()
    {
        return _httpContextAccessor.HttpContext?.User?.FindFirst(ClaimTypes.Email)?.Value;
    }

    /// <summary>
    /// Gets the current user's role from JWT token claims
    /// </summary>
    public string? GetUserRole()
    {
        return _httpContextAccessor.HttpContext?.User?.FindFirst(ClaimTypes.Role)?.Value;
    }

    /// <summary>
    /// Checks if the current user has a specific role
    /// </summary>
    public bool HasRole(string role)
    {
        return _httpContextAccessor.HttpContext?.User?.IsInRole(role) ?? false;
    }

    /// <summary>
    /// Checks if the current user has any of the specified roles
    /// </summary>
    public bool HasAnyRole(params string[] roles)
    {
        return roles.Any(role => HasRole(role));
    }
}
