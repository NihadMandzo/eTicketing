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
        return CreatedAtAction(nameof(GetByIdAsync), new { id = (result as dynamic)?.Id }, result);
    }

    [HttpPut("{id}")]
    public virtual async Task<IActionResult> UpdateAsync(int id, [FromBody] TUpdateRequest request, CancellationToken cancellationToken = default)
    {
        try
        {
            var result = await Service.UpdateAsync(id, request, cancellationToken);
            return Ok(result);
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
    }

    [HttpDelete("{id}")]
    public virtual async Task<IActionResult> DeleteAsync(int id, CancellationToken cancellationToken = default)
    {
        var result = await Service.DeleteAsync(id, cancellationToken);
        
        if (!result)
            return NotFound();
        
        return NoContent();
    }
}
