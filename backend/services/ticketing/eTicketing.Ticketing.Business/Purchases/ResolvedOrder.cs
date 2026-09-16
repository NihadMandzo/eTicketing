using System.Security.Claims;
using eTicketing.Contracts.Events;
using eTicketing.Contracts.Persistence;
using eTicketing.Contracts.Results;
using eTicketing.Ticketing.Business.External;
using eTicketing.Contracts.Security;
using eTicketing.Ticketing.Business.Security;
using eTicketing.Ticketing.Business.Sectors;
using eTicketing.Ticketing.Business.Tickets;
using eTicketing.Ticketing.Business.Time;
using eTicketing.Ticketing.Data.Entities;
using eTicketing.Ticketing.Data.Repositories;
using Microsoft.Extensions.Logging;

namespace eTicketing.Ticketing.Business.Purchases;

/// <summary>What a hold actually entitles the caller to buy, resolved server-side. Every field here
/// comes from Redis and the database, never from the request.</summary>
/// <param name="Product">The local read model of the eTicketing.Catalog product this sector belongs
/// to. Resolved once here because both jobs need it: refusing a purchase for a product that is no
/// longer published, and stamping the ticket's event name/date/city onto TicketPurchased so
/// eTicketing.PdfGeneration needs no catalogue client of its own.</param>
public record ResolvedOrder(
    HeldReservation Reservation,
    Sector Sector,
    ProductSnapshot Product,
    IReadOnlyDictionary<Guid, TicketType> TicketTypesById,
    decimal TotalPrice);
