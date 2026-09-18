using System.Globalization;
using System.Net;
using eTicketing.Contracts.Events;

namespace eTicketing.Notifications.Email.Templates;

/// <summary>
/// One template, two audiences — see <see cref="ProductDeletedAudience"/>. A buyer is told their
/// ticket is void and who to ask for their money back; an organization is told the platform
/// removed one of its products and how many buyers it now owes refunds to.
/// </summary>
/// <param name="OrganizerEmail">Null when Identity had no contact on file or was unreachable. The
/// contact block is then omitted entirely rather than printing an empty "Email:" line.</param>
public sealed record ProductDeletedData(
    string ProductName,
    ProductDeletedAudience Audience,
    DateTime? ProductDate,
    string OrganizerName,
    string? OrganizerEmail,
    string? OrganizerPhone,
    int TicketCount);
