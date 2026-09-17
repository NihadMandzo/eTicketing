namespace eTicketing.Shared.TicketPdf;

/// <summary>Where a buyer turns when something is wrong with their ticket. Configured per
/// deployment rather than hardcoded, and the phone is optional — a deployment with no support line
/// prints the address alone instead of an invented number.</summary>
public sealed record TicketSupportInfo(string Email, string? Phone)
{
    public string Display => string.IsNullOrWhiteSpace(Phone) ? Email : $"{Email} · {Phone}";
}
