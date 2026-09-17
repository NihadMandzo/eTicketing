namespace eTicketing.Contracts.Validation;

/// <summary>
/// Endpoint metadata recording which type a route declared <c>WithValidation&lt;T&gt;()</c> for,
/// so startup can verify an <see cref="FluentValidation.IValidator{T}"/> actually exists for it.
/// Without this the filter is unverifiable: it resolves its validator from the request container
/// and treats "none registered" the same as "nothing to complain about".
/// </summary>
/// <param name="RequestType">The <c>T</c> of the <see cref="ValidationFilter{T}"/> on the route.</param>
public sealed record ValidatedRequestMetadata(Type RequestType);
