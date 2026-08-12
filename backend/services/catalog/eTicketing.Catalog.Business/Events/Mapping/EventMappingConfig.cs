using eTicketing.Catalog.Data.Entities;
using Mapster;

namespace eTicketing.Catalog.Business.Events.Mapping;

public class EventMappingConfig : IRegister
{
    public void Register(TypeAdapterConfig config)
    {
        config.NewConfig<Event, EventResponse>()
            .Map(dest => dest.CategoryName, src => src.Category!.Name);
    }
}
