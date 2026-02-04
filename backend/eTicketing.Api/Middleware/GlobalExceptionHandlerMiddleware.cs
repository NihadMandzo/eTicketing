using System.Net;
using System.Text.Json;
using eTicketing.Api.Models;
using eTicketing.Api.Resources;
using eTicketing.Model.Exceptions;

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

    private Task HandleExceptionAsync(HttpContext context, Exception exception)
    {
        var traceId = context.TraceIdentifier;
        
        // Log exception details for ArgumentException and InvalidOperationException
        if (exception is ArgumentException argEx)
        {
            _logger.LogWarning(argEx, "Invalid argument: {Message}", argEx.Message);
        }
        else if (exception is InvalidOperationException invalidOpEx)
        {
            _logger.LogWarning(invalidOpEx, "Invalid operation: {Message}", invalidOpEx.Message);
        }
        
        // Determine status code and error details based on exception type
        var (statusCode, errorCode, message, errors) = exception switch
        {
            // 400 - Bad Request (Client errors)
            ValidationException validationEx => (
                StatusCodes.Status400BadRequest,
                "VALIDATION_ERROR",
                ErrorMessagesHr.ValidationFailed,
                validationEx.Errors
            ),
            DuplicateResourceException duplicateEx => (
                StatusCodes.Status409Conflict, // 409 is more appropriate for duplicates
                "DUPLICATE_RESOURCE",
                duplicateEx.Message,
                null
            ),
            ArgumentException => (
                StatusCodes.Status400BadRequest,
                "INVALID_ARGUMENT",
                ErrorMessagesHr.InvalidArgument,
                null
            ),
            BusinessLogicException businessEx => (
                StatusCodes.Status400BadRequest,
                "BUSINESS_LOGIC_ERROR",
                businessEx.Message,
                null
            ),
            InvalidOperationException => (
                StatusCodes.Status400BadRequest,
                "INVALID_OPERATION",
                ErrorMessagesHr.InvalidOperation,
                null
            ),
            
            // 401 & 403 - Authentication & Authorization
            UnauthorizedAccessException => (
                StatusCodes.Status403Forbidden,
                "FORBIDDEN",
                ErrorMessagesHr.AccessDenied,
                null
            ),
            
            // 404 - Not Found
            KeyNotFoundException => (
                StatusCodes.Status404NotFound,
                "NOT_FOUND",
                ErrorMessagesHr.ResourceNotFound,
                null
            ),
            
            // 500 - Internal Server Error (Server errors)
            _ => (
                StatusCodes.Status500InternalServerError,
                "INTERNAL_ERROR",
                ErrorMessagesHr.InternalServerError,
                null
            )
        };

        return WriteErrorResponseAsync(context, statusCode, errorCode, message, errors, traceId);
    }

    private static Task HandleStatusCodeAsync(HttpContext context)
    {
        var statusCode = context.Response.StatusCode;
        var traceId = context.TraceIdentifier;
        
        var (errorCode, message) = statusCode switch
        {
            StatusCodes.Status401Unauthorized => ("UNAUTHORIZED", ErrorMessagesHr.Unauthorized),
            StatusCodes.Status403Forbidden => ("FORBIDDEN", ErrorMessagesHr.AccessDenied),
            StatusCodes.Status404NotFound => ("NOT_FOUND", ErrorMessagesHr.ResourceNotFound),
            StatusCodes.Status400BadRequest => ("BAD_REQUEST", ErrorMessagesHr.InvalidRequest),
            _ => ("REQUEST_FAILED", ErrorMessagesHr.RequestFailed)
        };

        return WriteErrorResponseAsync(context, statusCode, errorCode, message, null, traceId);
    }

    private static Task WriteErrorResponseAsync(
        HttpContext context, 
        int statusCode, 
        string errorCode, 
        string message,
        IDictionary<string, string[]>? errors,
        string traceId)
    {
        context.Response.ContentType = "application/json";
        context.Response.StatusCode = statusCode;

        var response = new ErrorResponse
        {
            StatusCode = statusCode,
            ErrorCode = errorCode,
            Message = message,
            Errors = errors,
            TraceId = traceId
        };

        var jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
        };

        return context.Response.WriteAsync(JsonSerializer.Serialize(response, jsonOptions));
    }
}
