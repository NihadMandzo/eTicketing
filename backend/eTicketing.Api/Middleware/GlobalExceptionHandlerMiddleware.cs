using System.Net;
using System.Text.Json;
using eTicketing.Api.Models;

namespace eTicketing.Api.Middleware;

public class GlobalExceptionHandlerMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<GlobalExceptionHandlerMiddleware> _logger;

    public GlobalExceptionHandlerMiddleware(RequestDelegate next, ILogger<GlobalExceptionHandlerMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);

            // Handle error responses without body (404, 401, 403, etc.)
            if (!context.Response.HasStarted && context.Response.StatusCode >= 400)
            {
                await HandleStatusCodeAsync(context);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An unhandled exception occurred: {Message}", ex.Message);
            
            if (!context.Response.HasStarted)
            {
                await HandleExceptionAsync(context, ex);
            }
        }
    }

    private static Task HandleExceptionAsync(HttpContext context, Exception exception)
    {
        var statusCode = exception switch
        {
            KeyNotFoundException => StatusCodes.Status404NotFound,
            UnauthorizedAccessException => StatusCodes.Status403Forbidden,
            ArgumentException => StatusCodes.Status400BadRequest,
            InvalidOperationException => StatusCodes.Status400BadRequest,
            _ => StatusCodes.Status500InternalServerError
        };

        var message = exception switch
        {
            KeyNotFoundException => "Resurs nije pronađen",
            UnauthorizedAccessException => "Pristup odbijen",
            ArgumentException => "Neispravan zahtjev",
            InvalidOperationException => "Operacija nije dozvoljena",
            _ => "Došlo je do greške na serveru"
        };

        return WriteResponseAsync(context, statusCode, message);
    }

    private static Task HandleStatusCodeAsync(HttpContext context)
    {
        var statusCode = context.Response.StatusCode;
        
        var message = statusCode switch
        {
            StatusCodes.Status401Unauthorized => "Neautorizovan pristup",
            StatusCodes.Status403Forbidden => "Zabranjen pristup",
            StatusCodes.Status404NotFound => "Stranica nije pronađena",
            StatusCodes.Status400BadRequest => "Neispravan zahtjev",
            _ => "Zahtjev nije uspio"
        };

        return WriteResponseAsync(context, statusCode, message);
    }

    private static Task WriteResponseAsync(HttpContext context, int statusCode, string message)
    {
        context.Response.ContentType = "application/json";
        context.Response.StatusCode = statusCode;

        var response = new ApiResponse
        {
            StatusCode = statusCode,
            Message = message
        };

        var jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

        return context.Response.WriteAsync(JsonSerializer.Serialize(response, jsonOptions));
    }
}
