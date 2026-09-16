namespace eTicketing.Contracts.Tests.Validation;

/// <summary>A stand-in request type, so this test depends on no real service's DTOs.</summary>
public sealed record SampleQuery
{
    public int Size { get; init; }
}
