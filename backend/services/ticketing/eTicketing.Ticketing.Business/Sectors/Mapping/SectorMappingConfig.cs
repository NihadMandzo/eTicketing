using eTicketing.Ticketing.Data.Entities;
using Mapster;

namespace eTicketing.Ticketing.Business.Sectors.Mapping;

public class SectorMappingConfig : IRegister
{
    public void Register(TypeAdapterConfig config)
    {
        config.NewConfig<TicketType, TicketTypeResponse>();
        config.NewConfig<Sector, SectorResponse>()
            .Map(dest => dest.TicketTypes, src => src.TicketTypes.OrderBy(t => t.CreatedAt));
    }
}
