using eTicketing.Contracts.Validation;
using FluentAssertions;
using FluentValidation;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace eTicketing.Contracts.Tests.Validation;

/// <summary>A stand-in request type, so this test depends on no real service's DTOs.</summary>
public sealed record SampleQuery
{
    public int Size { get; init; }
}

public sealed class SampleQueryValidator : AbstractValidator<SampleQuery>
{
    public SampleQueryValidator() => RuleFor(x => x.Size).InclusiveBetween(1, 100);
}

/// <summary>
/// Pins the startup guard itself. Worth testing precisely because the bug it prevents is the
/// quiet kind: a route can declare <c>WithValidation&lt;T&gt;()</c> with no validator behind it
/// and keep answering 200 forever, enforcing nothing. Before this guard existed,
/// <c>GET /tickets/mine</c> had been in exactly that state — validated-looking, unvalidated.
/// </summary>
public class ValidatorRegistrationTests
{
    [Fact]
    public void VerifyRequestValidatorsRegistered_WhenAValidatorIsMissing_RefusesToStart()
    {
        var app = BuildApp(registerValidator: false);

        var act = () => app.VerifyRequestValidatorsRegistered();

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*SampleQuery*");
    }

    [Fact]
    public void VerifyRequestValidatorsRegistered_WhenEveryValidatorIsPresent_Starts()
    {
        var app = BuildApp(registerValidator: true);

        var act = () => app.VerifyRequestValidatorsRegistered();

        act.Should().NotThrow();
    }

    [Fact]
    public void VerifyRequestValidatorsRegistered_ForAnAppWithNoValidatedRoutes_Starts()
    {
        var builder = WebApplication.CreateBuilder();
        var app = builder.Build();
        app.MapGet("/health", () => "ok");

        var act = () => app.VerifyRequestValidatorsRegistered();

        act.Should().NotThrow();
    }

    private static WebApplication BuildApp(bool registerValidator)
    {
        var builder = WebApplication.CreateBuilder();
        if (registerValidator)
        {
            builder.Services.AddScoped<IValidator<SampleQuery>, SampleQueryValidator>();
        }

        var app = builder.Build();
        app.MapGet("/sample", ([AsParameters] SampleQuery query) => query.Size)
            .WithValidation<SampleQuery>();

        return app;
    }
}
