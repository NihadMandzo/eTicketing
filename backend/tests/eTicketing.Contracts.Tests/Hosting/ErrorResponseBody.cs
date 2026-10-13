namespace eTicketing.Contracts.Tests.Hosting;

/// <summary>The { code, message } shape every refusal on the platform answers with.</summary>
internal sealed record ErrorResponseBody(string Code, string Message);
