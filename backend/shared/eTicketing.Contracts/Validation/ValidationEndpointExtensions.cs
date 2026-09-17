using FluentValidation;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Http = Microsoft.AspNetCore.Http.Results;

namespace eTicketing.Contracts.Validation;

public static class ValidationEndpointExtensions
{
    /// <summary>Registers a <see cref="ValidationFilter{T}"/> for this endpoint, and records
    /// <typeparamref name="T"/> as endpoint metadata so
    /// <see cref="ValidatorRegistrationExtensions.VerifyRequestValidatorsRegistered"/> can prove at
    /// startup that a validator for it actually exists.</summary>
    public static RouteHandlerBuilder WithValidation<T>(this RouteHandlerBuilder builder)
        => builder
            .AddEndpointFilter<ValidationFilter<T>>()
            .WithMetadata(new ValidatedRequestMetadata(typeof(T)));
}
