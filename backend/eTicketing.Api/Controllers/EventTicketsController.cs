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
public class EventTicketsController : ControllerBase
{
    private readonly IEventTicketService _eventTicketService;

    public EventTicketsController(IEventTicketService eventTicketService)
    {
        _eventTicketService = eventTicketService;
    }

    [HttpGet]
    [AllowAnonymous]
    public async Task<IActionResult> GetAll([FromQuery] EventTicketSearchObject search)
    {
        var result = await _eventTicketService.GetAsync(search);
        return Ok(result);
    }

    [HttpGet("{id}")]
    [AllowAnonymous]
    public async Task<IActionResult> GetById(int id)
    {
        var result = await _eventTicketService.GetByIdAsync(id);
        
        if (result == null)
            return NotFound(new { message = ErrorMessages.TicketNotFound });
        
        return Ok(result);
    }

    [HttpGet("event/{eventId}")]
    [AllowAnonymous]
    public async Task<IActionResult> GetByEventId(int eventId)
    {
        var result = await _eventTicketService.GetByEventIdAsync(eventId);
        return Ok(result);
    }

    [HttpPost]
    [Authorize(Roles = "OrganizationSuperAdmin,OrganizationAdmin")]
    public async Task<IActionResult> Create([FromBody] EventTicketInsertRequest request)
    {
        // Ownership verification is enforced in the service layer (EventTicketService.BeforeCreateAsync)
        // which checks that the caller's organization matches the event's organization
        var result = await _eventTicketService.CreateAsync(request);
        return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
    }

    [HttpPut("{id}")]
    [Authorize(Roles = "OrganizationSuperAdmin,OrganizationAdmin")]
    public async Task<IActionResult> Update(int id, [FromBody] EventTicketUpdateRequest request)
    {
        // Ownership verification is enforced in the service layer (EventTicketService.BeforeUpdateAsync)
        // which checks that the caller's organization matches the ticket's organization
        var result = await _eventTicketService.UpdateAsync(id, request);
        
        if (result == null)
            return NotFound(new { message = ErrorMessages.TicketNotFound });
            
        return Ok(result);
    }

    [HttpDelete("{id}")]
    [Authorize(Roles = "OrganizationSuperAdmin,OrganizationAdmin")]
    public async Task<IActionResult> Delete(int id)
    {
        // Ownership verification is enforced in the service layer (EventTicketService.DeleteAsync)
        // which checks that the caller's organization matches the ticket's organization
        var result = await _eventTicketService.DeleteAsync(id);
        
        if (!result)
            return NotFound(new { message = ErrorMessages.TicketNotFound });
        
        return Ok(new { message = ErrorMessages.TicketDeletedSuccess });
    }
}
