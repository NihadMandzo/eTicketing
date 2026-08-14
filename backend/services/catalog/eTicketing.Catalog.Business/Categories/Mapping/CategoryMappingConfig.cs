using eTicketing.Catalog.Data.Entities;
using Mapster;

namespace eTicketing.Catalog.Business.Categories.Mapping;

/// <summary>
/// IconUrl has no matching source property on Category (it's derived from IconBlobName via
/// Azure Blob Storage, not stored) — left at its default null here and filled in by
/// CategoryService via `with` after mapping, same reasoning as OrganizationMappingConfig's
/// UserCount. IconBlobName itself also has no source on Create/UpdateCategoryRequest (icons are
/// managed exclusively through the dedicated icon-upload endpoints) — Mapster leaves it
/// untouched on update, and defaulted to null on create, with no explicit Ignore() needed.
/// </summary>
public class CategoryMappingConfig : IRegister
{
    public void Register(TypeAdapterConfig config)
    {
        config.NewConfig<Category, CategoryResponse>();
        config.NewConfig<CreateCategoryRequest, Category>();
        config.NewConfig<UpdateCategoryRequest, Category>();
    }
}
