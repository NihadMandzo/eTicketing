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

        // Add User to UserResponse mapping with RoleName from Role.Name
        CreateMap<User, UserResponse>()
            .ForMember(dest => dest.RoleName, opt => opt.MapFrom(src => src.Role.Name))
            .ForMember(dest => dest.OrganizationName, opt => opt.MapFrom(src => src.Organization != null ? src.Organization.Name : null));
    }
}
