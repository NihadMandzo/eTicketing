using eTicketing.Model.Requests;
using eTicketing.Model.Responses;
using eTicketing.Model.SearchObjects;
using eTicketing.Services.Interfaces.Shared;

namespace eTicketing.Services.Interfaces;

public interface IEventService : ICRUDService<EventResponse, EventSearchObject, EventInsertRequest, EventUpdateRequest>
{
}
