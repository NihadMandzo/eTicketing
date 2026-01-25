using eTicketing.Model.SearchObjects;
using eTicketing.Services.Interfaces.Shared;
using Microsoft.AspNetCore.Mvc;

namespace eTicketing.Api.Controllers.Shared;

public abstract class BaseCRUDController<TResponse, TSearch, TRequest, TUpdateRequest> 
    : BaseController<TResponse, TSearch>
    where TSearch : BaseSearchObject
{
    protected new readonly ICRUDService<TResponse, TSearch, TRequest, TUpdateRequest> Service;

    protected BaseCRUDController(ICRUDService<TResponse, TSearch, TRequest, TUpdateRequest> service) 
        : base(service)
    {
        Service = service;
    }

    [HttpPost]
    public virtual async Task<IActionResult> CreateAsync([FromBody] TRequest request, CancellationToken cancellationToken = default)
    {
        var result = await Service.CreateAsync(request, cancellationToken);
        return StatusCode(201, result);
    }

    [HttpPut("{id}")]
    public virtual async Task<IActionResult> UpdateAsync(int id, [FromBody] TUpdateRequest request, CancellationToken cancellationToken = default)
    {
        var result = await Service.UpdateAsync(id, request, cancellationToken);
        return Ok(result);
    }

    [HttpDelete("{id}")]
    public virtual async Task<IActionResult> DeleteAsync(int id, CancellationToken cancellationToken = default)
    {
        var result = await Service.DeleteAsync(id, cancellationToken);
        
        if (!result)
            return NotFound();
        
        return StatusCode(200, new { statusCode = 200, message = "Deleted successfully" });
    }
}
