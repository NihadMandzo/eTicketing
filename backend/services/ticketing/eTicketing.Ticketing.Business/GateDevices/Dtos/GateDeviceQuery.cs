using eTicketing.Contracts.Pagination;

namespace eTicketing.Ticketing.Business.GateDevices;

public sealed record GateDeviceQuery : BaseSearchObject
{
    public Guid? ProductId { get; init; }
}
