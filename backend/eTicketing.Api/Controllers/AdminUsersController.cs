using eTicketing.Api.Resources;
using eTicketing.Model.SearchObjects;
using eTicketing.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace eTicketing.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "SuperAdmin")]
public class AdminUsersController : ControllerBase
{
    private readonly IAdminUserService _adminUserService;

    public AdminUsersController(IAdminUserService adminUserService)
    {
        _adminUserService = adminUserService;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] AdminUserSearchObject search)
    {
        var result = await _adminUserService.GetAsync(search);
        return Ok(result);
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(int id)
    {
        var result = await _adminUserService.GetByIdAsync(id);

        if (result == null)
            return NotFound(new { message = ErrorMessagesHr.UserNotFound });

        return Ok(result);
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(int id)
    {
        var result = await _adminUserService.DeleteAsync(id);

        if (!result)
            return NotFound(new { message = ErrorMessagesHr.UserNotFound });

        return Ok(new { message = ErrorMessagesHr.AdminUserDeleteSuccess });
    }
}
