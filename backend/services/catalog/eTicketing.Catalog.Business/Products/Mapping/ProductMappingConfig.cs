using eTicketing.Catalog.Data.Entities;
using eTicketing.Catalog.Data.Repositories;
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

        // The internal shape eTicketing.Ticketing reads. Same Category dependency as above and the
        // same consequence if it is not loaded — TicketingMode would silently come back as
        // SingleOccurrence (the zero value), which is the mode every DailyEntry and
        // RecurringReservation validation branch is NOT.
        config.NewConfig<Product, ProductInternalResponse>()
            .Map(dest => dest.TicketingMode, src => src.Category!.TicketingMode);

        // Pure 1:1 over a repository projection, declared here rather than hand-written so the
        // Organizacije report's numbers are mapped the same way everything else in this service is.
        config.NewConfig<OrganizationProductStats, OrganizationProductStatsResponse>();
    }
}
