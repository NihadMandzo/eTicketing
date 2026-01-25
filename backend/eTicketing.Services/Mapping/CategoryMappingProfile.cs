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
        CreateMap<Category, CategoryResponse>();
        CreateMap<CategoryInsertRequest, Category>();
        CreateMap<CategoryUpdateRequest, Category>();
    }
}
