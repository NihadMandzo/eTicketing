using eTicketing.Api.Resources;
using eTicketing.Model.Requests;
using eTicketing.Model.SearchObjects;
using eTicketing.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace eTicketing.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class EventsController : ControllerBase
{
    private readonly IEventService _eventService;

    public EventsController(IEventService eventService)
    {
        _eventService = eventService;
    }

    [HttpGet]
    [AllowAnonymous]
    public async Task<IActionResult> GetAll([FromQuery] EventSearchObject search)
    {
        var result = await _eventService.GetAsync(search);
        return Ok(result);
    }

    [HttpGet("{id}")]
    [AllowAnonymous]
    public async Task<IActionResult> GetById(int id)
    {
        var result = await _eventService.GetByIdAsync(id);
        
        if (result == null)
            return NotFound(new { message = ErrorMessagesHr.EventNotFound });
        
        return Ok(result);
    }

    [HttpPost]
    [Authorize(Roles = "OrganizationSuperAdmin,OrganizationAdmin")]
    public async Task<IActionResult> Create([FromForm] EventInsertRequest request)
    {
        var result = await _eventService.CreateAsync(request);
        return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
    }

    [HttpPut("{id}")]
    [Authorize(Roles = "OrganizationSuperAdmin,OrganizationAdmin")]
    public async Task<IActionResult> Update(int id, [FromForm] EventUpdateRequest request)
    {
        var result = await _eventService.UpdateAsync(id, request);
        
        if (result == null)
            return NotFound(new { message = ErrorMessagesHr.EventNotFound });
            
        return Ok(result);
    }

    [HttpDelete("{id}")]
    [Authorize(Roles = "OrganizationSuperAdmin,OrganizationAdmin")]
    public async Task<IActionResult> Delete(int id)
    {
        var result = await _eventService.DeleteAsync(id);
        
        if (!result)
            return NotFound(new { message = ErrorMessagesHr.EventNotFound });
        
        return Ok(new { message = ErrorMessagesHr.EventDeleteSuccess });
    }
}
