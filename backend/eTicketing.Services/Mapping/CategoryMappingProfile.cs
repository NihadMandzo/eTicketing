using AutoMapper;
using eTicketing.Model.Requests;
using eTicketing.Model.Responses;
using eTicketing.Services.Database.Entities;

namespace eTicketing.Services.Mapping;

public class CategoryMappingProfile : Profile
{
    public CategoryMappingProfile()
    {
        // Category mappings
        CreateMap<Category, CategoryResponse>()
            .ForMember(dest => dest.IconUrl, opt => opt.MapFrom(src => src.Image != null ? src.Image.ImageUrl : null));
        
        CreateMap<CategoryInsertRequest, Category>()
            .ForMember(dest => dest.Image, opt => opt.Ignore())
            .ForMember(dest => dest.ImageId, opt => opt.Ignore());
        
        CreateMap<CategoryUpdateRequest, Category>()
            .ForMember(dest => dest.Image, opt => opt.Ignore())
            .ForMember(dest => dest.ImageId, opt => opt.Ignore());
    }
}
