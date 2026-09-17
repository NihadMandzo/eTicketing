using eTicketing.Contracts.Validation;
using FluentAssertions;
using FluentValidation;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace eTicketing.Contracts.Tests.Validation;

public sealed class SampleQueryValidator : AbstractValidator<SampleQuery>
{
    public SampleQueryValidator() => RuleFor(x => x.Size).InclusiveBetween(1, 100);
}
