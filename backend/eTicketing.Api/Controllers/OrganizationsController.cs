using eTicketing.Model.Requests;
using eTicketing.Model.SearchObjects;
using eTicketing.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace eTicketing.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class OrganizationsController : ControllerBase
{
    private readonly IOrganizationService _organizationService;

    public OrganizationsController(IOrganizationService organizationService)
    {
        _organizationService = organizationService;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] OrganizationSearchObject search)
    {
        var result = await _organizationService.GetAsync(search);
        return Ok(result);
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(int id)
    {
        var result = await _organizationService.GetByIdAsync(id);
        
        if (result == null)
            return NotFound(new { message = "Organizacija nije pronađena" });
        
        return Ok(result);
    }

    [HttpGet("{id}/detailed")]
    public async Task<IActionResult> GetByIdDetailed(int id)
    {
        var result = await _organizationService.GetByIdDetailedAsync(id);
        
        if (result == null)
            return NotFound(new { message = "Organizacija nije pronađena" });
            
        return Ok(result);
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] OrganizationInsertRequest request)
    {
        var result = await _organizationService.CreateAsync(request);
        return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(int id, [FromBody] OrganizationUpdateRequest request)
    {
        var result = await _organizationService.UpdateAsync(id, request);
        
        if (result == null)
            return NotFound(new { message = "Organizacija nije pronađena" });
            
        return Ok(result);
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(int id)
    {
        var result = await _organizationService.DeleteAsync(id);
        return result ? NoContent() : NotFound(new { message = "Organizacija nije pronađena" });
    }

    [HttpGet("{id}/users")]
    public async Task<IActionResult> GetOrganizationUsers(int id)
    {
        var result = await _organizationService.GetOrganizationUsersAsync(id);
        return Ok(result);
    }

    [HttpPost("{id}/users")]
    public async Task<IActionResult> AddUser(int id, [FromBody] OrganizationUserRequest request)
    {
        var result = await _organizationService.AddUserAsync(id, request);
        
        if (result == null)
            return NotFound(new { message = "Organizacija nije pronađena" });
            
        return CreatedAtAction(nameof(GetOrganizationUsers), new { id }, result);
    }

    [HttpDelete("{organizationId}/users/{userId}")]
    public async Task<IActionResult> RemoveUser(int organizationId, int userId)
    {
        var result = await _organizationService.RemoveUserAsync(organizationId, userId);
        return result ? NoContent() : NotFound(new { message = "Korisnik nije pronađen" });
    }
}
