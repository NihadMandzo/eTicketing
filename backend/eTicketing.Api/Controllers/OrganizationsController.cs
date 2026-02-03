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
    private readonly ILogger<OrganizationsController> _logger;

    public OrganizationsController(
        IOrganizationService organizationService,
        ILogger<OrganizationsController> logger)
    {
        _organizationService = organizationService;
        _logger = logger;
    }


    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] OrganizationSearchObject search)
    {
        try
        {
            var result = await _organizationService.GetAsync(search);
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving organizations");
            return StatusCode(500, new { message = "Došlo je do greške prilikom prikazivanja organizacija" });
        }
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(int id)
    {
        try
        {
            var result = await _organizationService.GetByIdAsync(id);
            return Ok(result);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving organization {OrganizationId}", id);
            return StatusCode(500, new { message = "Došlo je do greške prilikom prikazivanja organizacije" });        }
    }

    [HttpGet("{id}/detailed")]
    public async Task<IActionResult> GetByIdDetailed(int id)
    {
        try
        {
            var result = await _organizationService.GetByIdDetailedAsync(id);
            return Ok(result);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving detailed organization {OrganizationId}", id);
            return StatusCode(500, new { message = "Došlo je do greške prilikom prikazivanja organizacije" });
        }
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] OrganizationInsertRequest request)
    {
        try
        {
            var result = await _organizationService.CreateAsync(request);
            return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating organization");
            return StatusCode(500, new { message = "Došlo je do greške prilikom kreiranja organizacije" });
        }
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(int id, [FromBody] OrganizationUpdateRequest request)
    {
        try
        {
            var result = await _organizationService.UpdateAsync(id, request);
            return Ok(result);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating organization {OrganizationId}", id);
            return StatusCode(500, new { message = "Došlo je do greške prilikom ažuriranja organizacije" });
        }
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(int id)
    {
        try
        {
            var result = await _organizationService.DeleteAsync(id);
            return result ? NoContent() : NotFound(new { message = "Organizacija nije pronađena" });
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting organization {OrganizationId}", id);
            return StatusCode(500, new { message = "Došlo je do greške prilikom brisanja organizacije" });
        }
    }


    [HttpGet("{id}/users")]
    public async Task<IActionResult> GetOrganizationUsers(int id)
    {
        try
        {
            var result = await _organizationService.GetOrganizationUsersAsync(id);
            return Ok(result);
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving users for organization {OrganizationId}", id);
            return StatusCode(500, new { message = "Došlo je do greške prilikom preuzimanja korisnika organizacije" });
        }
    }


    [HttpPost("{id}/users")]
    public async Task<IActionResult> AddUser(int id, [FromBody] OrganizationUserRequest request)
    {
        try
        {
            var result = await _organizationService.AddUserAsync(id, request);
            return CreatedAtAction(nameof(GetOrganizationUsers), new { id }, result);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error adding user to organization {OrganizationId}", id);
            return StatusCode(500, new { message = "Došlo je do greške prilikom dodavanja korisnika" });
        }
    }

    [HttpDelete("{organizationId}/users/{userId}")]
    public async Task<IActionResult> RemoveUser(int organizationId, int userId)
    {
        try
        {
            var result = await _organizationService.RemoveUserAsync(organizationId, userId);
            return result ? NoContent() : NotFound(new { message = "Korisnik nije pronađen" });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error removing user {UserId} from organization {OrganizationId}", userId, organizationId);
            return StatusCode(500, new { message = "Došlo je do greške prilikom uklanjanja korisnika" });
        }
    }
}
