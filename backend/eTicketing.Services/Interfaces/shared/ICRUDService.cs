using eTicketing.Model.SearchObjects;

namespace eTicketing.Services.Interfaces.Shared;

public interface ICRUDService<TResponse, TSearch, TRequest, TUpdateRequest> : IService<TResponse, TSearch>
    where TSearch : BaseSearchObject
{
    Task<TResponse> CreateAsync(TRequest request, CancellationToken cancellationToken = default);
    Task<TResponse> UpdateAsync(int id, TUpdateRequest request, CancellationToken cancellationToken = default);
    Task<bool> DeleteAsync(int id, CancellationToken cancellationToken = default);
}
