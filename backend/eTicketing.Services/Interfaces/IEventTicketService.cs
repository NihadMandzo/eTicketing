using eTicketing.Model.Requests;
using eTicketing.Model.Responses;
using eTicketing.Model.SearchObjects;
using eTicketing.Services.Interfaces.Shared;

namespace eTicketing.Services.Interfaces;

public interface IEventTicketService : ICRUDService<EventTicketResponse, EventTicketSearchObject, EventTicketInsertRequest, EventTicketUpdateRequest>
{
    Task<List<EventTicketResponse>> GetByOrganizationIdAsync(int organizationId, CancellationToken cancellationToken = default);
    Task<List<EventTicketResponse>> GetByEventIdAsync(int eventId, CancellationToken cancellationToken = default);
    Task<bool> ValidateTicketAvailabilityAsync(int ticketId, int quantity, CancellationToken cancellationToken = default);
}
