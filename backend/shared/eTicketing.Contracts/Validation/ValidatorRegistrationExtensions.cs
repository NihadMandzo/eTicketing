using FluentValidation;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace eTicketing.Contracts.Validation;

public static class ValidatorRegistrationExtensions
{
    /// <summary>
    /// Fails startup if any endpoint declares <c>WithValidation&lt;T&gt;()</c> without a
    /// matching <see cref="IValidator{T}"/> in the container.
    /// </summary>
    /// <remarks>
    /// <para>This exists because the failure it catches is silent by nature.
    /// <see cref="ValidationFilter{T}"/> resolves its validator with <c>GetService</c>, so a type
    /// whose validator was never written looks exactly like a type with nothing to validate — the
    /// route keeps answering 200 and simply enforces no rules. That is how
    /// <c>GET /tickets/mine</c> ended up with no PageSize ceiling while *looking* validated at the
    /// call site.</para>
    /// <para>Deliberately a startup check rather than switching the filter to
    /// <c>GetRequiredService</c>: throwing on the first request just moves the discovery to
    /// production traffic, and only for routes someone happens to call. Here, the service refuses
    /// to boot at all — the whole point of a validator is that it runs before anything else does.
    /// Call it after the endpoints are mapped and before <c>app.Run()</c>.</para>
    /// </remarks>
    /// <exception cref="InvalidOperationException">Lists every route type missing a validator.</exception>
    public static WebApplication VerifyRequestValidatorsRegistered(this WebApplication app)
    {
        // The app's own data sources, not EndpointDataSource from DI: the container resolves a
        // composite that is still empty at this point in startup, so reading it would make this
        // check silently pass for every service — the exact failure mode it exists to prevent.
        var declared = ((IEndpointRouteBuilder)app).DataSources
            .SelectMany(source => source.Endpoints)
            .SelectMany(endpoint => endpoint.Metadata.GetOrderedMetadata<ValidatedRequestMetadata>())
            .Select(metadata => metadata.RequestType)
            .Distinct()
            .ToList();

        using var scope = app.Services.CreateScope();

        var missing = declared
            .Where(requestType =>
                scope.ServiceProvider.GetService(typeof(IValidator<>).MakeGenericType(requestType)) is null)
            .Select(requestType => requestType.Name)
            .OrderBy(name => name)
            .ToList();

        if (missing.Count > 0)
        {
            throw new InvalidOperationException(
                $"Nedostaje FluentValidation validator za: {string.Join(", ", missing)}. " +
                "Svaki tip proslijeđen u WithValidation<T>() mora imati registrovan IValidator<T> " +
                "(AddValidatorsFromAssembly ga pokupi automatski iz odgovarajućeg .Business projekta).");
        }

        return app;
    }
}
