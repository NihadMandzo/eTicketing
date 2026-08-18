using eTicketing.Catalog.Data.Entities;
using Mapster;

namespace eTicketing.Catalog.Business.Products.Mapping;

/// <summary>Requires Product.Category to be loaded (via Include or an explicit assignment after
/// insert) — CategoryName/TicketingMode are read off the navigation, not a second query.</summary>
public class ProductMappingConfig : IRegister
{
    public void Register(TypeAdapterConfig config)
    {
        config.NewConfig<Product, ProductResponse>()
            .Map(dest => dest.CategoryName, src => src.Category!.Name)
            .Map(dest => dest.TicketingMode, src => src.Category!.TicketingMode);
    }
}
