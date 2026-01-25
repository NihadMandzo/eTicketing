using eTicketing.Api.Models;
using Microsoft.AspNetCore.Mvc;

namespace eTicketing.Api.Extensions;

public static class ControllerExtensions
{
    public static IActionResult Success(this ControllerBase controller, object data, int statusCode = 200)
    {
        return new ObjectResult(data)
        {
            StatusCode = statusCode
        };
    }

    public static IActionResult Error(this ControllerBase controller, string message, int statusCode = 400)
    {
        return new ObjectResult(new ApiResponse
        {
            StatusCode = statusCode,
            Message = message
        })
        {
            StatusCode = statusCode
        };
    }

    public static IActionResult BadRequest(this ControllerBase controller, string message)
    {
        return controller.Error(message, StatusCodes.Status400BadRequest);
    }

    public static IActionResult Unauthorized(this ControllerBase controller, string message = "Unauthorized")
    {
        return controller.Error(message, StatusCodes.Status401Unauthorized);
    }

    public static IActionResult Forbidden(this ControllerBase controller, string message = "Forbidden")
    {
        return controller.Error(message, StatusCodes.Status403Forbidden);
    }

    public static IActionResult NotFoundError(this ControllerBase controller, string message = "Not found")
    {
        return controller.Error(message, StatusCodes.Status404NotFound);
    }

    public static IActionResult ServerError(this ControllerBase controller, string message = "Internal server error")
    {
        return controller.Error(message, StatusCodes.Status500InternalServerError);
    }
}
