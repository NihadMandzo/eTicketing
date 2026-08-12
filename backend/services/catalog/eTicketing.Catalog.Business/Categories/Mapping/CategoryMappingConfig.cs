using eTicketing.Catalog.Data.Entities;
using Mapster;

namespace eTicketing.Catalog.Business.Categories.Mapping;

/// <summary>
/// IconUrl has no matching source property on Category (it's derived from Id, not stored) —
/// left at its default "" here and filled in by CategoryService via `with` after mapping,
/// same reasoning as OrganizationMappingConfig's UserCount.
/// </summary>
public class CategoryMappingConfig : IRegister
{
    public void Register(TypeAdapterConfig config)
    {
        config.NewConfig<Category, CategoryResponse>();

        config.NewConfig<CreateCategoryRequest, Category>()
            .Ignore(dest => dest.IconData)
            .Ignore(dest => dest.IconContentType);

        config.NewConfig<UpdateCategoryRequest, Category>()
            .Ignore(dest => dest.IconData)
            .Ignore(dest => dest.IconContentType);
    }
}
