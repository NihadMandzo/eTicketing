namespace eTicketing.Shared.TicketPdf;

/// <summary>Contact details printed in the green footer of every ticket. Configurable per
/// deployment because the address a buyer should write to is not a property of the code.</summary>
public sealed class TicketSupportOptions
{
    public const string SectionName = "TicketSupport";

    public string Email { get; set; } = "podrska@ekarta.ba";

    /// <summary>Optional — a deployment without a support line prints the address alone rather than
    /// an invented number.</summary>
    public string? Phone { get; set; }
}
