using eTicketing.Model.SearchObjects;
using eTicketing.Services.Interfaces.Shared;
using Microsoft.AspNetCore.Mvc;

namespace eTicketing.Api.Controllers.Shared;

[ApiController]
[Route("api/[controller]")]
public abstract class BaseController<TResponse, TSearch> : ControllerBase
    where TSearch : BaseSearchObject
{
    protected readonly IService<TResponse, TSearch> Service;

    protected BaseController(IService<TResponse, TSearch> service)
    {
        Service = service;
    }

    [HttpGet]
    public virtual async Task<IActionResult> GetAsync([FromQuery] TSearch? search, CancellationToken cancellationToken = default)
    {
        var result = await Service.GetAsync(search, cancellationToken);
        return Ok(result);
    }

    [HttpGet("{id}")]
    public virtual async Task<IActionResult> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        var result = await Service.GetByIdAsync(id, cancellationToken);
        
        if (result == null)
            return NotFound();
        
        return Ok(result);
    }
}
