using eTicketing.Api.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace eTicketing.Api.Filters;

public class ApiResponseFilter : IActionFilter
{
    public void OnActionExecuting(ActionExecutingContext context)
    {
        // Validate model state
        if (!context.ModelState.IsValid)
        {
            var errors = context.ModelState.Values
                .SelectMany(v => v.Errors)
                .Select(e => e.ErrorMessage)
                .ToList();

            var errorMessage = string.Join("; ", errors);

            context.Result = new ObjectResult(new ApiResponse
            {
                StatusCode = StatusCodes.Status400BadRequest,
                Message = errorMessage
            })
            {
                StatusCode = StatusCodes.Status400BadRequest
            };
        }
    }

    public void OnActionExecuted(ActionExecutedContext context)
    {
        // Wrap successful responses in ApiResponse format
        if (context.Result is ObjectResult objectResult && objectResult.Value != null)
        {
            var statusCode = objectResult.StatusCode ?? StatusCodes.Status200OK;

            // Only wrap if it's not already an ApiResponse
            if (objectResult.Value is not ApiResponse)
            {
                // For 200 and 201, return the data as-is (don't wrap in ApiResponse)
                // This allows GET requests to return the actual data
                if (statusCode == StatusCodes.Status200OK || statusCode == StatusCodes.Status201Created)
                {
                    return;
                }
            }
        }

        // Handle NotFoundResult
        if (context.Result is NotFoundResult)
        {
            context.Result = new ObjectResult(new ApiResponse
            {
                StatusCode = StatusCodes.Status404NotFound,
                Message = "Not found"
            })
            {
                StatusCode = StatusCodes.Status404NotFound
            };
        }

        // Handle NotFoundObjectResult
        if (context.Result is NotFoundObjectResult notFoundObject)
        {
            var message = notFoundObject.Value?.ToString() ?? "Not found";
            context.Result = new ObjectResult(new ApiResponse
            {
                StatusCode = StatusCodes.Status404NotFound,
                Message = message
            })
            {
                StatusCode = StatusCodes.Status404NotFound
            };
        }

        // Handle UnauthorizedResult
        if (context.Result is UnauthorizedResult)
        {
            context.Result = new ObjectResult(new ApiResponse
            {
                StatusCode = StatusCodes.Status401Unauthorized,
                Message = "Unauthorized"
            })
            {
                StatusCode = StatusCodes.Status401Unauthorized
            };
        }

        // Handle ForbidResult
        if (context.Result is ForbidResult)
        {
            context.Result = new ObjectResult(new ApiResponse
            {
                StatusCode = StatusCodes.Status403Forbidden,
                Message = "Forbidden"
            })
            {
                StatusCode = StatusCodes.Status403Forbidden
            };
        }

        // Handle BadRequestResult
        if (context.Result is BadRequestResult)
        {
            context.Result = new ObjectResult(new ApiResponse
            {
                StatusCode = StatusCodes.Status400BadRequest,
                Message = "Bad request"
            })
            {
                StatusCode = StatusCodes.Status400BadRequest
            };
        }

        // Handle BadRequestObjectResult
        if (context.Result is BadRequestObjectResult badRequestObject)
        {
            var message = badRequestObject.Value?.ToString() ?? "Bad request";
            context.Result = new ObjectResult(new ApiResponse
            {
                StatusCode = StatusCodes.Status400BadRequest,
                Message = message
            })
            {
                StatusCode = StatusCodes.Status400BadRequest
            };
        }
    }
}
