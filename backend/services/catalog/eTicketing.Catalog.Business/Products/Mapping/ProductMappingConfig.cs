using eTicketing.Catalog.Data.Entities;
using Mapster;

namespace eTicketing.Catalog.Business.Products.Mapping;

/// <summary>Requires Product.Category to be loaded (via Include or an explicit assignment after
/// insert) — CategoryName/TicketingMode are read off the navigation, not a second query.
/// Images is deliberately ignored here (ProductImage has no Url to map from — it's derived from
/// Azure Blob Storage) and always filled in by ProductService.ToResponse instead, same reasoning
/// as CategoryService.ToResponse/BuildIconUrl.</summary>
public class ProductMappingConfig : IRegister
{
    public void Register(TypeAdapterConfig config)
    {
        config.NewConfig<Product, ProductResponse>()
            .Map(dest => dest.CategoryName, src => src.Category!.Name)
            .Map(dest => dest.TicketingMode, src => src.Category!.TicketingMode)
            .Ignore(dest => dest.Images);
    }
}
