using AutoMapper;
using eTicketing.Services.Database.Entities;

namespace eTicketing.Services.Mapping;

/// <summary>
/// Example AutoMapper profile - Create similar profiles for your entities
/// </summary>
public class ExampleMappingProfile : Profile
{
    public ExampleMappingProfile()
    {
        // Example mappings - uncomment and modify when you create your DTOs

        // Category mappings
        // CreateMap<Category, CategoryResponse>();
        // CreateMap<CategoryRequest, Category>();
        // CreateMap<CategoryUpdateRequest, Category>();

        // User mappings
        // CreateMap<User, UserResponse>()
        //     .ForMember(dest => dest.RoleName, opt => opt.MapFrom(src => src.Role.Name));
        // CreateMap<UserRequest, User>()
        //     .ForMember(dest => dest.PasswordHash, opt => opt.Ignore())
        //     .ForMember(dest => dest.PasswordSalt, opt => opt.Ignore());

        // Organization mappings
        // CreateMap<Organization, OrganizationResponse>();
        // CreateMap<OrganizationRequest, Organization>();
        // CreateMap<OrganizationUpdateRequest, Organization>();
    }
}
