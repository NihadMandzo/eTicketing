using eTicketing.Api.Resources;
using eTicketing.Model.Requests;
using eTicketing.Model.SearchObjects;
using eTicketing.Services.Interfaces;
using eTicketing.Services.Helpers;
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
            return NotFound(new { message = "Ticket not found" });
        
        return Ok(result);
    }

    [HttpGet("event/{eventId}")]
    [AllowAnonymous]
    public async Task<IActionResult> GetByEventId(int eventId)
    {
        var result = await _eventTicketService.GetByEventIdAsync(eventId);
        return Ok(result);
    }

    [HttpGet("organization/{organizationId}")]
    [Authorize(Roles = "SuperAdmin,Admin,OrganizationSuperAdmin,OrganizationAdmin")]
    public async Task<IActionResult> GetByOrganizationId(int organizationId)
    {
        
        var result = await _eventTicketService.GetByOrganizationIdAsync(organizationId);
        return Ok(result);
    }

    [HttpGet("{id}/validate-availability")]
    [AllowAnonymous]
    public async Task<IActionResult> ValidateAvailability(int id, [FromQuery] int quantity = 1)
    {
        // Validate quantity parameter
        if (quantity < 1)
        {
            return BadRequest(new { message = "Quantity must be at least 1" });
        }
        
        var isAvailable = await _eventTicketService.ValidateTicketAvailabilityAsync(id, quantity);
        return Ok(new { available = isAvailable });
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
            return NotFound(new { message = "Ticket not found" });
            
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
            return NotFound(new { message = "Ticket not found" });
        
        return Ok(new { message = "Ticket deleted successfully" });
    }
}
