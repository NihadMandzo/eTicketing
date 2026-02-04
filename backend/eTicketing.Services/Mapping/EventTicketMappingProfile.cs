using AutoMapper;
using eTicketing.Model.Requests;
using eTicketing.Model.Responses;
using eTicketing.Services.Database.Entities;

namespace eTicketing.Services.Mapping;

public class EventTicketMappingProfile : Profile
{
    public EventTicketMappingProfile()
    {
        CreateMap<EventTicket, EventTicketResponse>()
            .ForMember(dest => dest.TicketsRemaining, opt => opt.MapFrom(src => src.TicketsRemaining))
            .ForMember(dest => dest.EventTitle, opt => opt.MapFrom(src => src.Event != null ? src.Event.Title : string.Empty))
            .ForMember(dest => dest.OrganizationName, opt => opt.MapFrom(src => src.Organization != null ? src.Organization.Name : string.Empty));
        
        CreateMap<EventTicketInsertRequest, EventTicket>()
            .ForMember(dest => dest.OrganizationId, opt => opt.Ignore()) // Set in service
            .ForMember(dest => dest.TicketsSold, opt => opt.Ignore()); // Always starts at 0
        
        CreateMap<EventTicketUpdateRequest, EventTicket>()
            .ForMember(dest => dest.EventId, opt => opt.Ignore()) // Should not be changed on update
            .ForMember(dest => dest.OrganizationId, opt => opt.Ignore()) // Should not be changed on update
            .ForMember(dest => dest.TicketsSold, opt => opt.Ignore()); // Should not be changed via update request
    }
}
