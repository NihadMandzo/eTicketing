using eTicketing.Contracts.Events;
using eTicketing.Contracts.Persistence;
using eTicketing.Ticketing.Business.External;
using eTicketing.Ticketing.Business.Time;
using eTicketing.Ticketing.Data.Repositories;
using Microsoft.Extensions.Logging;

namespace eTicketing.Ticketing.Business.Integration;

/// <summary>
/// Turns "a product was deleted" into the emails that deletion owes people, for the same reason
/// <see cref="ProductChangeNotifier"/> exists: eTicketing.Catalog performs the delete but has no
/// idea who bought a ticket, eTicketing.Ticketing knows every buyer but never sees the delete, and
/// eTicketing.Notifications stays a pure renderer that is handed already-resolved addresses.
///
/// <para>Two audiences, deliberately worded differently downstream:</para>
/// <list type="bullet">
/// <item>every buyer holding a still-valid ticket — their ticket is void and they need to know who
/// to ask for their money back;</item>
/// <item>the owning organization, but <b>only</b> when platform staff did the deleting. An
/// organizer who deletes their own product does not need an email telling them so.</item>
/// </list>
///
/// <para>The refund contact is the organization's own address, not the platform's: the organizer
/// took the payment, so the organizer issues the refund.</para>
/// </summary>
public class ProductDeletionNotifier : IProductDeletionNotifier
{
    private readonly ITicketRepository _ticketRepository;
    private readonly IIdentityClient _identityClient;
    private readonly IEventPublisher _eventPublisher;
    private readonly IUnitOfWork _unitOfWork;
    private readonly PlatformClock _clock;
    private readonly ILogger<ProductDeletionNotifier> _logger;

    public ProductDeletionNotifier(
        ITicketRepository ticketRepository,
        IIdentityClient identityClient,
        IEventPublisher eventPublisher,
        IUnitOfWork unitOfWork,
        PlatformClock clock,
        ILogger<ProductDeletionNotifier> logger)
    {
        _ticketRepository = ticketRepository;
        _identityClient = identityClient;
        _eventPublisher = eventPublisher;
        _unitOfWork = unitOfWork;
        _clock = clock;
        _logger = logger;
    }

    public async Task NotifyAsync(ProductDeleted message, CancellationToken ct = default)
    {
        // Local business day, not UTC — see PlatformClock. A ticket valid "today" locally must not
        // be treated as expired because UTC has not rolled over yet.
        var today = _clock.Today();
        var buyers = await _ticketRepository.GetLiveBuyerTicketCountsForProductAsync(message.ProductId, today, ct);

        var contact = await ResolveContactAsync(message.OrganizationId, ct);

        foreach (var buyer in buyers)
        {
            await _eventPublisher.PublishAsync(
                EventNames.ProductDeletedNotification,
                new ProductDeletedNotification(
                    ProductId: message.ProductId,
                    ProductName: message.ProductName,
                    RecipientEmail: buyer.UserEmail,
                    Audience: ProductDeletedAudience.Buyer,
                    ProductDate: message.ProductDate,
                    OrganizerName: contact?.Name ?? "Organizator",
                    OrganizerEmail: contact?.Email,
                    OrganizerPhone: contact?.PhoneNumber,
                    TicketCount: buyer.TicketCount),
                ct);
        }

        if (message.DeletedByPlatformStaff)
        {
            await NotifyOrganizationAsync(message, contact, buyers.Count, ct);
        }

        // These publishes write outbox rows rather than reaching the broker, and a row that is
        // never saved is an event that silently never happens. This class has no domain write of
        // its own — it is a pure fan-out — so the save that commits them has to be explicit. One
        // call covering both audiences, after the organization notice too, so a deletion's whole
        // set of emails either goes out or none of it does.
        await _unitOfWork.SaveChangesAsync(ct);

        _logger.LogInformation(
            "Proizvod {ProductId} je obrisan — obavještenje poslano za {BuyerCount} kupaca{Organizer}.",
            message.ProductId,
            buyers.Count,
            message.DeletedByPlatformStaff ? " i organizaciju" : string.Empty);
    }

    /// <summary>
    /// The "platform staff removed your product" notice. Addressed to the organization's
    /// OrganizationSuperAdmin where there is one, falling back to the organization's own contact
    /// address — an organization whose super admin was deleted still has a mailbox worth telling.
    /// </summary>
    private async Task NotifyOrganizationAsync(
        ProductDeleted message, IdentityOrganizationContactResponse? contact, int buyerCount, CancellationToken ct)
    {
        var recipient = FirstNonEmpty(contact?.SuperAdminEmail, contact?.Email);
        if (recipient is null)
        {
            // Nothing to do but say so: the product is already gone, and failing here would only
            // park a message that can never succeed.
            _logger.LogWarning(
                "Proizvod {ProductId} je obrisan od strane platforme, ali organizacija {OrganizationId} nema kontakt adresu — obavještenje nije poslano.",
                message.ProductId, message.OrganizationId);
            return;
        }

        await _eventPublisher.PublishAsync(
            EventNames.ProductDeletedNotification,
            new ProductDeletedNotification(
                ProductId: message.ProductId,
                ProductName: message.ProductName,
                RecipientEmail: recipient,
                Audience: ProductDeletedAudience.Organizer,
                ProductDate: message.ProductDate,
                OrganizerName: contact?.Name ?? "Vaša organizacija",
                OrganizerEmail: contact?.Email,
                OrganizerPhone: contact?.PhoneNumber,
                // How many buyers the organization now has to refund — the one number that makes
                // this notice actionable rather than merely informative.
                TicketCount: buyerCount),
            ct);
    }

    /// <summary>
    /// Identity being unreachable must not swallow the cancellation. A buyer learning their event
    /// is off without a contact line is far better served than a buyer learning nothing because a
    /// lookup for a phone number failed, so this degrades to null rather than throwing.
    /// </summary>
    private async Task<IdentityOrganizationContactResponse?> ResolveContactAsync(
        Guid organizationId, CancellationToken ct)
    {
        try
        {
            return await _identityClient.GetOrganizationContactAsync(organizationId, ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Neuspješno dohvaćanje kontakt podataka organizacije {OrganizationId} — obavještenja se šalju bez kontakta.",
                organizationId);
            return null;
        }
    }

    private static string? FirstNonEmpty(params string?[] candidates) =>
        candidates.FirstOrDefault(c => !string.IsNullOrWhiteSpace(c));
}
