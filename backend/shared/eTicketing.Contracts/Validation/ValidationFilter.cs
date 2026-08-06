using FluentValidation;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Http = Microsoft.AspNetCore.Http.Results;

namespace eTicketing.Contracts.Validation;

/// <summary>
/// Generic minimal-API endpoint filter that runs the registered FluentValidation
/// <see cref="IValidator{T}"/> (if any) against a bound request/query argument of type
/// <typeparamref name="T"/>, short-circuiting with a standard ASP.NET Core
/// ValidationProblem (field-level errors) when invalid.
/// Works for JSON-body parameters and for complex types bound via [AsParameters] alike —
/// both surface in <see cref="EndpointFilterInvocationContext.Arguments"/>.
///
/// Note: this project also has a namespace called "Results" (eTicketing.Contracts.Results),
/// so — same as ResultExtensions.cs — the ASP.NET Core Results class is referenced via the
/// "Http" alias here to avoid the name collision.
/// </summary>
public class ValidationFilter<T> : IEndpointFilter
{
    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        var argument = context.Arguments.OfType<T>().FirstOrDefault();
        if (argument is not null)
        {
            var validator = context.HttpContext.RequestServices.GetService<IValidator<T>>();
            if (validator is not null)
            {
                var validationResult = await validator.ValidateAsync(argument);
                if (!validationResult.IsValid)
                {
                    return Http.ValidationProblem(validationResult.ToDictionary());
                }
            }
        }

        return await next(context);
    }
}

public static class ValidationEndpointExtensions
{
    /// <summary>Registers a <see cref="ValidationFilter{T}"/> for this endpoint.</summary>
    public static RouteHandlerBuilder WithValidation<T>(this RouteHandlerBuilder builder)
        => builder.AddEndpointFilter<ValidationFilter<T>>();
}
