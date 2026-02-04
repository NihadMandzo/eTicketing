using eTicketing.Model.Requests;
using eTicketing.Model.Responses;
using eTicketing.Model.SearchObjects;
using eTicketing.Services.Interfaces.Shared;

namespace eTicketing.Services.Interfaces;

public interface IEventTicketService : ICRUDService<EventTicketResponse, EventTicketSearchObject, EventTicketInsertRequest, EventTicketUpdateRequest>
{
    Task<List<EventTicketResponse>> GetByEventIdAsync(int eventId, CancellationToken cancellationToken = default);
}
