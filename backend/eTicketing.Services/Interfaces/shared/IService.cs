using eTicketing.Model.Responses;
using eTicketing.Model.SearchObjects;

namespace eTicketing.Services.Interfaces.Shared;

public interface IService<TResponse, TSearch> 
    where TSearch : BaseSearchObject
{
    Task<PagedResponse<TResponse>> GetAsync(TSearch? search = null, CancellationToken cancellationToken = default);
    Task<TResponse?> GetByIdAsync(int id, CancellationToken cancellationToken = default);
}
