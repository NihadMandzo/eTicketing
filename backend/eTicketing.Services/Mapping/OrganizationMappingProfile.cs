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
            .ForMember(dest => dest.LogoUrl, opt => opt.MapFrom(src => src.Image != null ? src.Image.ImageUrl : null))
            .ForMember(dest => dest.UserCount, opt => opt.Ignore());
        
        CreateMap<Organization, OrganizationDetailResponse>()
            .ForMember(dest => dest.LogoUrl, opt => opt.MapFrom(src => src.Image != null ? src.Image.ImageUrl : null))
            .ForMember(dest => dest.UserCount, opt => opt.Ignore())
            .ForMember(dest => dest.Administrators, opt => opt.Ignore());
        
        CreateMap<OrganizationInsertRequest, Organization>()
            .ForMember(dest => dest.Image, opt => opt.Ignore())
            .ForMember(dest => dest.ImageId, opt => opt.Ignore());
        CreateMap<OrganizationUpdateRequest, Organization>()
            .ForMember(dest => dest.Image, opt => opt.Ignore())
            .ForMember(dest => dest.ImageId, opt => opt.Ignore());
    }
}
