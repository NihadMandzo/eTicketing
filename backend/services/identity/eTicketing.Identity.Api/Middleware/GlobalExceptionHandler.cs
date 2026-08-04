using Microsoft.AspNetCore.Diagnostics;

namespace eTicketing.Identity.Api.Middleware;

public class GlobalExceptionHandler : IExceptionHandler
{
    private readonly ILogger<GlobalExceptionHandler> _logger;

    public GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger) => _logger = logger;

    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken ct)
    {
        _logger.LogError(exception, "Neobrađen izuzetak na {Path}", httpContext.Request.Path);

        httpContext.Response.StatusCode = StatusCodes.Status500InternalServerError;
        await httpContext.Response.WriteAsJsonAsync(new
        {
            code = "internal_error",
            message = "Došlo je do neočekivane greške. Pokušajte kasnije."
        }, cancellationToken: ct);

        return true;
    }
}
