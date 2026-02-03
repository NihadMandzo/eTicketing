using AutoMapper;
using eTicketing.Model.Requests;
using eTicketing.Model.Responses;
using eTicketing.Services.Database.Entities;

namespace eTicketing.Services.Mapping;

public class EventMappingProfile : Profile
{
    public EventMappingProfile()
    {
        CreateMap<Event, EventResponse>()
            .ForMember(dest => dest.OrganizationName, opt => opt.MapFrom(src => src.Organization.Name))
            .ForMember(dest => dest.Images, opt => opt.Ignore()); // Handled in service
        
        CreateMap<EventInsertRequest, Event>()
            .ForMember(dest => dest.Images, opt => opt.Ignore()); // Handled in service
        
        CreateMap<EventUpdateRequest, Event>()
            .ForMember(dest => dest.Images, opt => opt.Ignore()) // Handled in service
            .ForMember(dest => dest.OrganizationId, opt => opt.Ignore()); // Should not be changed on update
        
        CreateMap<EventImage, EventImageResponse>();
    }
}
