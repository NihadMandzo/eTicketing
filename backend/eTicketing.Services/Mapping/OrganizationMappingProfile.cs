using AutoMapper;
using eTicketing.Model.Requests;
using eTicketing.Model.Responses;
using eTicketing.Services.Database.Entities;

namespace eTicketing.Services.Mapping;

public class OrganizationMappingProfile : Profile
{
    public OrganizationMappingProfile()
    {
        CreateMap<Organization, OrganizationResponse>()
            .ForMember(dest => dest.UserCount, opt => opt.Ignore());
        
        CreateMap<Organization, OrganizationDetailResponse>()
            .ForMember(dest => dest.UserCount, opt => opt.Ignore())
            .ForMember(dest => dest.Administrators, opt => opt.Ignore());
        
        CreateMap<OrganizationInsertRequest, Organization>();
        CreateMap<OrganizationUpdateRequest, Organization>();
    }
}
