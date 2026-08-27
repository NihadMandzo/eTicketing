using eTicketing.Contracts.Pagination;
using FluentValidation;

namespace eTicketing.Ticketing.Business.GateDevices.Validators;

public class GateDeviceQueryValidator : AbstractValidator<GateDeviceQuery>
{
    public GateDeviceQueryValidator()
    {
        Include(new BaseSearchObjectValidator<GateDeviceQuery>());
    }
}
