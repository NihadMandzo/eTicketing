using Microsoft.AspNetCore.Http;

namespace eTicketing.Services.Helpers;

/// <summary>
/// Helper class for HTTP-related operations
/// </summary>
public class HttpHelper
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public HttpHelper(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    /// <summary>
    /// Gets the JWT token from the Authorization header
    /// </summary>
    /// <returns>The JWT token without the "Bearer " prefix, or null if not found</returns>
    public string? GetJwtToken()
    {
        var authorizationHeader = _httpContextAccessor.HttpContext?.Request.Headers["Authorization"].FirstOrDefault();

        if (string.IsNullOrWhiteSpace(authorizationHeader))
            return null;

        // Remove "Bearer " prefix if present
        if (authorizationHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
        {
            return authorizationHeader.Substring("Bearer ".Length).Trim();
        }

        return authorizationHeader;
    }

    /// <summary>
    /// Gets the full Authorization header value
    /// </summary>
    public string? GetAuthorizationHeader()
    {
        return _httpContextAccessor.HttpContext?.Request.Headers["Authorization"].FirstOrDefault();
    }

    /// <summary>
    /// Gets a header value by name
    /// </summary>
    public string? GetHeaderValue(string headerName)
    {
        return _httpContextAccessor.HttpContext?.Request.Headers[headerName].FirstOrDefault();
    }

    /// <summary>
    /// Gets the client IP address
    /// </summary>
    public string? GetClientIpAddress()
    {
        var httpContext = _httpContextAccessor.HttpContext;
        if (httpContext == null)
            return null;

        // Check for forwarded IP address (when behind proxy/load balancer)
        var forwardedFor = httpContext.Request.Headers["X-Forwarded-For"].FirstOrDefault();
        if (!string.IsNullOrEmpty(forwardedFor))
        {
            var ips = forwardedFor.Split(',');
            return ips[0].Trim();
        }

        return httpContext.Connection.RemoteIpAddress?.ToString();
    }

    /// <summary>
    /// Gets the request User-Agent
    /// </summary>
    public string? GetUserAgent()
    {
        return _httpContextAccessor.HttpContext?.Request.Headers["User-Agent"].FirstOrDefault();
    }

    /// <summary>
    /// Gets the base URL of the current request
    /// </summary>
    public string? GetBaseUrl()
    {
        var request = _httpContextAccessor.HttpContext?.Request;
        if (request == null)
            return null;

        return $"{request.Scheme}://{request.Host}{request.PathBase}";
    }

    /// <summary>
    /// Checks if the request is authenticated (has a valid JWT token)
    /// </summary>
    public bool IsAuthenticated()
    {
        return _httpContextAccessor.HttpContext?.User?.Identity?.IsAuthenticated ?? false;
    }

    /// <summary>
    /// Gets all headers as a dictionary
    /// </summary>
    public Dictionary<string, string> GetAllHeaders()
    {
        var headers = _httpContextAccessor.HttpContext?.Request.Headers;
        if (headers == null)
            return new Dictionary<string, string>();

        return headers.ToDictionary(h => h.Key, h => h.Value.ToString());
    }
}
