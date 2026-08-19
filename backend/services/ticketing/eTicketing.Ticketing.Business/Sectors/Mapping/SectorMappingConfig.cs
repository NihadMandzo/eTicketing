using eTicketing.Ticketing.Data.Entities;
using Mapster;

namespace eTicketing.Ticketing.Business.Sectors.Mapping;

public class SectorMappingConfig : IRegister
{
    public void Register(TypeAdapterConfig config)
    {
        config.NewConfig<Sector, SectorResponse>();
    }
}
